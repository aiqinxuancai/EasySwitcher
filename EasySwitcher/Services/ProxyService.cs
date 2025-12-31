using System.Diagnostics;
using System.Net;
using EasySwitcher.Config;
using EasySwitcher.Runtime;

namespace EasySwitcher.Services;

public sealed class ProxyService
{
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade",
    };

    private static readonly HashSet<string> DefaultKeyHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "X-Api-Key",
        "Api-Key",
        "X-Google-Api-Key",
    };

    private readonly AppConfig _config;
    private readonly LoadBalancer _loadBalancer;
    private readonly HealthTracker _health;
    private readonly RequestLogger _logger;
    private readonly HttpClient _httpClient;
    private readonly HashSet<string> _knownGroups;

    public ProxyService(AppConfig config, LoadBalancer loadBalancer, HealthTracker health, RequestLogger logger)
    {
        _config = config;
        _loadBalancer = loadBalancer;
        _health = health;
        _logger = logger;
        _knownGroups = BuildKnownGroups(config);
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!IsAuthorized(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var (group, forwardPath) = ResolveGroupAndPath(context);
        if (!_config.Groups.TryGetValue(group, out var groupConfig))
        {
            groupConfig = null;
        }

        var now = DateTimeOffset.UtcNow;
        var candidates = _loadBalancer.GetCandidates(group, groupConfig, _config, now);
        if (candidates.Count == 0)
        {
            var hasGroupPlatforms = _config.Platforms.Any(p => p.Enabled &&
                                                               string.Equals(p.Group, group, StringComparison.OrdinalIgnoreCase));
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            var message = hasGroupPlatforms ? "暂无健康上游。" : "未配置上游。";
            await context.Response.WriteAsync(message, context.RequestAborted);
            return;
        }

        var timeoutSeconds = groupConfig?.TimeoutSeconds ?? _config.Server.TimeoutSeconds;
        var attemptLimit = Math.Min(groupConfig?.MaxFailover ?? _config.Server.MaxFailover, candidates.Count);
        attemptLimit = Math.Max(attemptLimit, 1);

        var bufferedBody = await RequestBodyBuffer.TryBufferAsync(context.Request, _config.Server.MaxRequestBodyBytes, context.RequestAborted);

        for (var attempt = 0; attempt < attemptLimit; attempt++)
        {
            if (attempt > 0 && !bufferedBody.CanRetry)
            {
                break;
            }

            var platform = candidates[attempt];
            var attemptStopwatch = Stopwatch.StartNew();
            var statusCode = StatusCodes.Status502BadGateway;

            try
            {
                using var requestMessage = BuildRequestMessage(context, platform, bufferedBody, forwardPath);
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, timeoutCts.Token);

                using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
                statusCode = (int)response.StatusCode;

                if (_health.IsRetryableStatusCode(statusCode) && attempt + 1 < attemptLimit && !context.Response.HasStarted)
                {
                    ReportFailure(platform, group, $"状态码 {statusCode}");
                    LogAttempt(context, platform, group, statusCode, attemptStopwatch, attempt + 1, attemptLimit, false, "可重试状态码");
                    continue;
                }

                context.Response.StatusCode = statusCode;
                CopyResponseHeaders(context, response);
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);

                var success = statusCode < 400;
                if (success)
                {
                    _health.ReportSuccess(platform);
                }
                else
                {
                    ReportFailure(platform, group, $"状态码 {statusCode}");
                }

                LogAttempt(context, platform, group, statusCode, attemptStopwatch, attempt + 1, attemptLimit, success, null);
                return;
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
            {
                statusCode = StatusCodes.Status504GatewayTimeout;
                ReportFailure(platform, group, "超时");
                LogAttempt(context, platform, group, statusCode, attemptStopwatch, attempt + 1, attemptLimit, false, "超时");
            }
            catch (Exception ex)
            {
                ReportFailure(platform, group, "异常");
                LogAttempt(context, platform, group, statusCode, attemptStopwatch, attempt + 1, attemptLimit, false, ex.Message);
            }

            if (context.Response.HasStarted)
            {
                return;
            }
        }

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("暂无健康上游。", context.RequestAborted);
        }
    }

    private bool IsAuthorized(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("Authorization", out var authValue))
        {
            var auth = authValue.ToString();
            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = auth.Substring("Bearer ".Length);
                return string.Equals(token, _config.Server.AuthKey, StringComparison.Ordinal);
            }
        }

        return false;
    }

    private (string Group, PathString ForwardPath) ResolveGroupAndPath(HttpContext context)
    {
        var requestPath = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
        if (string.IsNullOrWhiteSpace(requestPath) || requestPath == "/")
        {
            return (_config.Server.DefaultGroup, context.Request.Path);
        }

        var trimmed = requestPath.TrimStart('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return (_config.Server.DefaultGroup, new PathString("/"));
        }

        var slashIndex = trimmed.IndexOf('/');
        var firstSegment = slashIndex >= 0 ? trimmed[..slashIndex] : trimmed;

        if (_knownGroups.Contains(firstSegment))
        {
            var remaining = slashIndex >= 0 ? trimmed[slashIndex..] : string.Empty;
            if (string.IsNullOrEmpty(remaining))
            {
                remaining = "/";
            }

            return (firstSegment, new PathString(remaining));
        }

        return (_config.Server.DefaultGroup, context.Request.Path);
    }

    private HttpRequestMessage BuildRequestMessage(HttpContext context, PlatformState platform, BufferedRequestBody bufferedBody, PathString forwardPath)
    {
        var targetUri = BuildTargetUri(platform.BaseUri, forwardPath, context.Request.QueryString);
        var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);

        var hasBody = bufferedBody.Body is { Length: > 0 } ||
                      (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > 0) ||
                      context.Request.Headers.ContainsKey("Transfer-Encoding");

        if (hasBody)
        {
            if (bufferedBody.Body is null)
            {
                requestMessage.Content = new StreamContent(context.Request.Body);
            }
            else
            {
                requestMessage.Content = new ByteArrayContent(bufferedBody.Body);
            }
        }

        CopyRequestHeaders(context, platform, requestMessage);
        ApplyPlatformKey(platform, requestMessage);
        requestMessage.Headers.Host = platform.BaseUri.Host;

        return requestMessage;
    }

    private static Uri BuildTargetUri(Uri baseUri, PathString path, QueryString query)
    {
        var basePath = baseUri.AbsolutePath.TrimEnd('/');
        var requestPath = path.HasValue ? path.Value! : "/";
        if (!requestPath.StartsWith("/", StringComparison.Ordinal))
        {
            requestPath = "/" + requestPath;
        }

        var combinedPath = string.IsNullOrEmpty(basePath) || basePath == "/"
            ? requestPath
            : $"{basePath}{requestPath}";

        var builder = new UriBuilder(baseUri)
        {
            Path = combinedPath,
            Query = query.HasValue ? query.Value!.TrimStart('?') : string.Empty,
        };
        return builder.Uri;
    }

    private static HashSet<string> BuildKnownGroups(AppConfig config)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(config.Server.DefaultGroup))
        {
            groups.Add(config.Server.DefaultGroup);
        }

        foreach (var group in config.Groups.Keys)
        {
            if (!string.IsNullOrWhiteSpace(group))
            {
                groups.Add(group);
            }
        }

        foreach (var platform in config.Platforms)
        {
            if (!string.IsNullOrWhiteSpace(platform.Group))
            {
                groups.Add(platform.Group);
            }
        }

        return groups;
    }

    private static void CopyRequestHeaders(HttpContext context, PlatformState platform, HttpRequestMessage requestMessage)
    {
        var keyHeader = platform.Config.KeyHeader ?? "Authorization";

        foreach (var header in context.Request.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key))
            {
                continue;
            }

            if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (DefaultKeyHeaders.Contains(header.Key) || string.Equals(header.Key, keyHeader, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) &&
                requestMessage.Content is not null)
            {
                requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }
    }

    private static void ApplyPlatformKey(PlatformState platform, HttpRequestMessage requestMessage)
    {
        if (string.IsNullOrWhiteSpace(platform.Config.ApiKey))
        {
            return;
        }

        var header = platform.Config.KeyHeader ?? "Authorization";
        var prefix = platform.Config.KeyPrefix ?? string.Empty;
        var value = $"{prefix}{platform.Config.ApiKey}";

        if (!requestMessage.Headers.TryAddWithoutValidation(header, value) && requestMessage.Content is not null)
        {
            requestMessage.Content.Headers.TryAddWithoutValidation(header, value);
        }
    }

    private static void CopyResponseHeaders(HttpContext context, HttpResponseMessage response)
    {
        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in response.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        context.Response.Headers.Remove("transfer-encoding");
    }

    private void LogAttempt(HttpContext context, PlatformState platform, string group, int statusCode, Stopwatch stopwatch, int attempt, int attemptLimit, bool success, string? error)
    {
        stopwatch.Stop();
        var entry = new ProxyLogEntry(
            DateTimeOffset.UtcNow,
            group,
            platform.Config.Name,
            context.Request.Method,
            $"{context.Request.Path}{context.Request.QueryString}",
            statusCode,
            stopwatch.ElapsedMilliseconds,
            success,
            attempt,
            attemptLimit,
            error);
        _logger.Log(entry);
    }

    private void ReportFailure(PlatformState platform, string group, string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var circuit = _health.ReportFailure(platform, now);
        if (circuit is null)
        {
            return;
        }

        _logger.LogCircuitBreak(new CircuitBreakLogEntry(
            now,
            group,
            platform.Config.Name,
            reason,
            circuit.Cooldown,
            circuit.Until,
            circuit.TripCount));
    }
}

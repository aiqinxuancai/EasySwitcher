using Spectre.Console;

namespace EasySwitcher.Services;

public sealed class RequestLogger
{
    public void Log(ProxyLogEntry entry)
    {
        var statusColor = entry.Success ? "green" : "red";
        var statusText = entry.Success ? "成功" : "失败";
        var attemptText = entry.AttemptLimit > 1 ? $"（尝试 {entry.Attempt}/{entry.AttemptLimit}）" : string.Empty;
        var attemptSuffix = string.IsNullOrWhiteSpace(attemptText) ? string.Empty : $" {attemptText}";
        var message = $"[grey]{entry.Timestamp:O}[/] [blue]{Markup.Escape(entry.Group)}[/] [cyan]{Markup.Escape(entry.Platform)}[/] " +
                      $"{Markup.Escape(entry.Method)} {Markup.Escape(entry.PathAndQuery)} " +
                      $"[{statusColor}]{entry.StatusCode} {statusText}[/] {entry.ElapsedMilliseconds}ms{attemptSuffix}";

        if (!string.IsNullOrWhiteSpace(entry.Error))
        {
            message += $" [red]原因: {Markup.Escape(entry.Error)}[/]";
        }

        AnsiConsole.MarkupLine(message);
    }

    public void LogCircuitBreak(CircuitBreakLogEntry entry)
    {
        var cooldownSeconds = Math.Max(0, (int)Math.Round(entry.Cooldown.TotalSeconds));
        var reason = string.IsNullOrWhiteSpace(entry.Reason) ? "未知" : entry.Reason;
        var message = $"[grey]{entry.Timestamp:O}[/] [red]熔断[/] [blue]{Markup.Escape(entry.Group)}[/] " +
                      $"[cyan]{Markup.Escape(entry.Platform)}[/] 原因: {Markup.Escape(reason)} " +
                      $"冷却: {cooldownSeconds}s 直到: {entry.Until:O} 连续熔断: {entry.TripCount}";
        AnsiConsole.MarkupLine(message);
    }
}

public sealed record ProxyLogEntry(
    DateTimeOffset Timestamp,
    string Group,
    string Platform,
    string Method,
    string PathAndQuery,
    int StatusCode,
    long ElapsedMilliseconds,
    bool Success,
    int Attempt,
    int AttemptLimit,
    string? Error);

public sealed record CircuitBreakLogEntry(
    DateTimeOffset Timestamp,
    string Group,
    string Platform,
    string Reason,
    TimeSpan Cooldown,
    DateTimeOffset Until,
    int TripCount);

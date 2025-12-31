using EasySwitcher.Config;

namespace EasySwitcher.Runtime;

public sealed class PlatformState
{
    private readonly object _lock = new();

    public PlatformState(PlatformConfig config, Uri baseUri)
    {
        Config = config;
        BaseUri = baseUri;
    }

    public PlatformConfig Config { get; }
    public Uri BaseUri { get; }

    public int FailureCount { get; private set; }
    public int CircuitBreakCount { get; private set; }
    public DateTimeOffset? UnhealthyUntil { get; private set; }

    public void ReportSuccess()
    {
        lock (_lock)
        {
            FailureCount = 0;
            CircuitBreakCount = 0;
            UnhealthyUntil = null;
        }
    }

    public CircuitBreakResult? ReportFailure(int threshold, TimeSpan baseCooldown, DateTimeOffset now)
    {
        lock (_lock)
        {
            FailureCount++;
            if (FailureCount >= threshold)
            {
                FailureCount = 0;
                CircuitBreakCount++;
                var cooldown = CalculateCooldown(baseCooldown, CircuitBreakCount);
                UnhealthyUntil = now.Add(cooldown);
                return new CircuitBreakResult(true, cooldown, UnhealthyUntil.Value, CircuitBreakCount);
            }
        }

        return null;
    }

    public bool IsHealthy(DateTimeOffset now)
    {
        lock (_lock)
        {
            return UnhealthyUntil is null || UnhealthyUntil <= now;
        }
    }

    private static TimeSpan CalculateCooldown(TimeSpan baseCooldown, int multiplierPower)
    {
        var baseSeconds = Math.Max(0, baseCooldown.TotalSeconds);
        if (baseSeconds <= 0)
        {
            return TimeSpan.Zero;
        }

        var exponent = Math.Max(0, Math.Min(multiplierPower - 1, 10));
        var multiplier = Math.Pow(2, exponent);
        var seconds = Math.Min(baseSeconds * multiplier, TimeSpan.MaxValue.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}

public sealed record CircuitBreakResult(bool Tripped, TimeSpan Cooldown, DateTimeOffset Until, int TripCount);

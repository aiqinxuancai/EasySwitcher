namespace EasySwitcher.Config;

public sealed class HealthConfig
{
    /// <summary>
    /// 连续失败次数达到该值后标记为不健康。
    /// </summary>
    public int FailureThreshold { get; set; } = 2;
    /// <summary>
    /// 不健康冷却时间（秒）。
    /// </summary>
    public int CooldownSeconds { get; set; } = 30;
    /// <summary>
    /// 可重试状态码最小值。
    /// </summary>
    public int RetryableStatusMin { get; set; } = 500;
    /// <summary>
    /// 可重试状态码最大值。
    /// </summary>
    public int RetryableStatusMax { get; set; } = 599;
    /// <summary>
    /// 是否将 429 视为可重试。
    /// </summary>
    public bool RetryOn429 { get; set; } = true;
}

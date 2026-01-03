namespace AviSwitch.Config;

public sealed class HealthConfig
{
    /// <summary>
    /// 连续 400+ 响应或请求异常/超时次数达到该值后标记为不健康。
    /// </summary>
    public int FailureThreshold { get; set; } = 2;
    /// <summary>
    /// 不健康冷却时间（秒，连续熔断按倍数增加）。
    /// </summary>
    public int CooldownSeconds { get; set; } = 30;
}

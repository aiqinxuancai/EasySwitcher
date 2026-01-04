namespace AviSwitch.Config;

public sealed class ServerConfig
{
    /// <summary>
    /// 服务监听地址。
    /// </summary>
    public string Listen { get; set; } = "http://0.0.0.0:7085";
    /// <summary>
    /// 外部访问鉴权 Key。
    /// </summary>
    public string AuthKey { get; set; } = string.Empty;
    /// <summary>
    /// 默认分组名称。
    /// </summary>
    public string DefaultGroup { get; set; } = "default";
    /// <summary>
    /// 默认负载均衡策略（weighted 或 failover）。
    /// </summary>
    public string Strategy { get; set; } = "weighted";
    /// <summary>
    /// 上游请求超时（秒）。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 600;
    /// <summary>
    /// 触发熔断冷却的连续失败次数。
    /// </summary>
    public int MaxFailover { get; set; } = 2;
    /// <summary>
    /// 可重试请求体的最大缓存大小（字节）。
    /// </summary>
    public int MaxRequestBodyBytes { get; set; } = 100 * 1024 * 1024;
    /// <summary>
    /// 开启DEBUG日志
    /// </summary>
    public bool DebugLog { get; set; } = false;
}

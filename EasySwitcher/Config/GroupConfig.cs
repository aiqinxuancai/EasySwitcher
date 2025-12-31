namespace EasySwitcher.Config;

public sealed class GroupConfig
{
    /// <summary>
    /// 分组负载均衡策略覆盖。
    /// </summary>
    public string? Strategy { get; set; }
    /// <summary>
    /// 分组故障转移尝试次数覆盖。
    /// </summary>
    public int? MaxFailover { get; set; }
    /// <summary>
    /// 分组超时（秒）覆盖。
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}

namespace AviSwitch.Config;

public sealed class PlatformConfig
{
    /// <summary>
    /// 平台名称（日志显示）。
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// 上游基础地址。
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
    /// <summary>
    /// 上游 API Key。
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>
    /// 所属分组名称。
    /// </summary>
    public string Group { get; set; } = string.Empty;
    /// <summary>
    /// 权重（越大越容易被选中）。
    /// </summary>
    public int Weight { get; set; } = 1;
    /// <summary>
    /// 优先级（越小越优先，故障转移使用）。
    /// </summary>
    public int Priority { get; set; } = 0;
    /// <summary>
    /// Predefined key injection type: openai, claude, or gemini.
    /// </summary>
    public string? KeyType { get; set; }
    /// <summary>
    /// 注入 API Key 的请求头名称。
    /// </summary>
    public string? KeyHeader { get; set; }
    /// <summary>
    /// API Key 前缀（如 "Bearer "）。
    /// </summary>
    public string? KeyPrefix { get; set; }
    /// <summary>
    /// 是否启用该平台。
    /// </summary>
    public bool Enabled { get; set; } = true;
}

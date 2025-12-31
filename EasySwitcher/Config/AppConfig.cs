namespace EasySwitcher.Config;

public sealed class AppConfig
{
    /// <summary>
    /// 服务端配置。
    /// </summary>
    public ServerConfig Server { get; set; } = new();
    /// <summary>
    /// 健康检测配置。
    /// </summary>
    public HealthConfig Health { get; set; } = new();
    /// <summary>
    /// 上游平台列表。
    /// </summary>
    public List<PlatformConfig> Platforms { get; set; } = new();
    /// <summary>
    /// 分组覆盖配置，键为分组名。
    /// </summary>
    public Dictionary<string, GroupConfig> Groups { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void ApplyDefaultsAndValidate()
    {
        Server ??= new ServerConfig();
        Health ??= new HealthConfig();
        Platforms ??= new List<PlatformConfig>();
        Groups ??= new Dictionary<string, GroupConfig>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(Server.Listen))
        {
            Server.Listen = "http://0.0.0.0:7085";
        }

        if (string.IsNullOrWhiteSpace(Server.AuthKey))
        {
            throw new InvalidOperationException("server.auth_key is required.");
        }

        if (string.IsNullOrWhiteSpace(Server.DefaultGroup))
        {
            Server.DefaultGroup = "default";
        }

        if (string.IsNullOrWhiteSpace(Server.Strategy))
        {
            Server.Strategy = "round_robin";
        }

        if (Server.TimeoutSeconds <= 0)
        {
            Server.TimeoutSeconds = 600;
        }

        if (Server.MaxFailover <= 0)
        {
            Server.MaxFailover = 1;
        }

        if (Server.MaxRequestBodyBytes <= 0)
        {
            Server.MaxRequestBodyBytes = 10 * 1024 * 1024;
        }

        if (Health.FailureThreshold <= 0)
        {
            Health.FailureThreshold = 2;
        }

        if (Health.CooldownSeconds <= 0)
        {
            Health.CooldownSeconds = 30;
        }

        if (Platforms.Count == 0)
        {
            throw new InvalidOperationException("At least one platform is required.");
        }

        var platformIndex = 1;
        foreach (var platform in Platforms)
        {
            if (string.IsNullOrWhiteSpace(platform.Name))
            {
                platform.Name = $"platform-{platformIndex}";
            }

            if (string.IsNullOrWhiteSpace(platform.BaseUrl))
            {
                throw new InvalidOperationException($"platforms[{platformIndex}].base_url is required.");
            }

            if (string.IsNullOrWhiteSpace(platform.Group))
            {
                platform.Group = Server.DefaultGroup;
            }

            if (platform.Weight <= 0)
            {
                platform.Weight = 1;
            }

            if (platform.KeyHeader is null)
            {
                platform.KeyHeader = "Authorization";
            }

            if (platform.KeyPrefix is null)
            {
                platform.KeyPrefix = "Bearer ";
            }

            platformIndex++;
        }

        var normalizedGroups = new Dictionary<string, GroupConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, group) in Groups)
        {
            var normalized = string.IsNullOrWhiteSpace(key) ? Server.DefaultGroup : key;
            normalizedGroups[normalized] = group ?? new GroupConfig();
        }

        Groups = normalizedGroups;
    }
}

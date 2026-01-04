using AviSwitch.Config;
using Spectre.Console;

namespace AviSwitch.Services;

public static class StartupReporter
{
    public static void Print(AppConfig config, string configPath)
    {
        AnsiConsole.Write(new FigletText("AviSwitch").Color(Color.Cyan));
        AnsiConsole.MarkupLine($"[green]AviSwitch[/] 已加载配置: [blue]{Markup.Escape(configPath)}[/]");

        // 先计算每个分组的最小优先级（主节点）
        var groupMinPriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var platform in config.Platforms)
        {
            if (!groupMinPriority.ContainsKey(platform.Group))
            {
                groupMinPriority[platform.Group] = platform.Priority;
            }
            else if (platform.Priority < groupMinPriority[platform.Group])
            {
                groupMinPriority[platform.Group] = platform.Priority;
            }
        }

        var groupOrder = BuildGroupOrder(config);
        foreach (var group in groupOrder)
        {
            var groupPlatforms = config.Platforms
                .Where(p => string.Equals(p.Group, group, StringComparison.OrdinalIgnoreCase))
                .ToList();

            config.Groups.TryGetValue(group, out var groupConfig);
            var strategy = (groupConfig?.Strategy ?? config.Server.Strategy).Trim();
            var maxFailover = groupConfig?.MaxFailover ?? config.Server.MaxFailover;
            var timeout = groupConfig?.TimeoutSeconds ?? config.Server.TimeoutSeconds;
            var platformCount = config.Platforms.Count(p => p.Enabled &&
                                                           string.Equals(p.Group, group, StringComparison.OrdinalIgnoreCase));

            AnsiConsole.MarkupLine(
                $"[yellow]分组:[/] {Markup.Escape(group)}  " +
                $"[grey]策略:[/] {Markup.Escape(FormatStrategy(strategy))}  " +
                $"[grey]熔断阈值:[/] {maxFailover}  " +
                $"[grey]超时:[/] {timeout}s  " +
                $"[grey]平台数:[/] {platformCount}");

            if (groupPlatforms.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]无平台[/]");
                continue;
            }

            var table = new Table();
            table.AddColumn("名称");
            table.AddColumn("优先级");
            table.AddColumn("权重");
            table.AddColumn("启用");
            table.AddColumn("上游地址");

            foreach (var platform in groupPlatforms)
            {
                // 获取该平台所属分组的策略
                var platformStrategy = strategy.Trim().ToLowerInvariant();

                // 根据策略和优先级决定颜色
                string color = GetPriorityColor(platformStrategy, platform.Priority);

                // 在 failover 模式下，为最低优先级（主节点）添加标记
                string priorityText = platform.Priority.ToString();
                if (platformStrategy == "failover" &&
                    groupMinPriority.TryGetValue(platform.Group, out var minPriority) &&
                    platform.Priority == minPriority)
                {
                    priorityText = $"{platform.Priority}[主]";
                }

                table.AddRow(
                    $"[{color}]{Markup.Escape(platform.Name)}[/]",
                    $"[{color}]{Markup.Escape(priorityText)}[/]",
                    $"[{color}]{platform.Weight}[/]",
                    $"[{color}]{(platform.Enabled ? "是" : "否")}[/]",
                    $"[{color}]{Markup.Escape(platform.BaseUrl)}[/]");
            }

            AnsiConsole.Write(table);
        }

        AnsiConsole.MarkupLine($"[grey]监听地址:[/] {Markup.Escape(config.Server.Listen)}");
        AnsiConsole.MarkupLine($"[grey]默认分组:[/] {Markup.Escape(config.Server.DefaultGroup)}");
        AnsiConsole.MarkupLine($"[grey]默认策略:[/] {Markup.Escape(FormatStrategy(config.Server.Strategy))}  [grey]默认超时:[/] {config.Server.TimeoutSeconds}s  [grey]熔断阈值:[/] {config.Server.MaxFailover}");
        AnsiConsole.MarkupLine($"[grey]基础冷却:[/] {config.Health.CooldownSeconds}s");
    }

    private static string FormatStrategy(string strategy)
    {
        var normalized = strategy.Trim().ToLowerInvariant();
        return normalized switch
        {
            "weighted" => "加权轮询",
            "failover" => "主备",
            _ => strategy,
        };
    }

    /// <summary>
    /// 根据策略和优先级返回对应的颜色
    /// 主备模式下，不同优先级使用不同颜色；加权模式下使用默认颜色
    /// </summary>
    private static string GetPriorityColor(string strategy, int priority)
    {
        // 只有在 failover 策略下才根据优先级区分颜色
        if (strategy != "failover")
        {
            return "white";
        }

        // failover 模式下，根据优先级返回不同颜色
        return priority switch
        {
            0 => "green",      // 主节点 - 绿色
            1 => "yellow",     // 备1 - 黄色
            2 => "orange1",    // 备2 - 橙色
            3 => "darkorange", // 备3 - 深橙色
            _ => "red"         // 备4+ - 红色
        };
    }

    private static List<string> BuildGroupOrder(AppConfig config)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddGroup(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (seen.Add(name))
            {
                ordered.Add(name);
            }
        }

        AddGroup(config.Server.DefaultGroup);

        foreach (var platform in config.Platforms)
        {
            AddGroup(platform.Group);
        }

        foreach (var group in config.Groups.Keys)
        {
            AddGroup(group);
        }

        return ordered;
    }
}

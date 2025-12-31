using EasySwitcher.Config;
using Spectre.Console;

namespace EasySwitcher.Services;

public static class StartupReporter
{
    public static void Print(AppConfig config, string configPath)
    {
        AnsiConsole.MarkupLine($"[green]EasySwitcher[/] 已加载配置: [blue]{Markup.Escape(configPath)}[/]");

        var table = new Table();
        table.AddColumn("名称");
        table.AddColumn("分组");
        table.AddColumn("优先级");
        table.AddColumn("权重");
        table.AddColumn("上游地址");

        foreach (var platform in config.Platforms)
        {
            table.AddRow(
                Markup.Escape(platform.Name),
                Markup.Escape(platform.Group),
                platform.Priority.ToString(),
                platform.Weight.ToString(),
                Markup.Escape(platform.BaseUrl));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]监听地址:[/] {Markup.Escape(config.Server.Listen)}");
        AnsiConsole.MarkupLine($"[grey]默认分组:[/] {Markup.Escape(config.Server.DefaultGroup)}");
        AnsiConsole.MarkupLine($"[grey]默认策略:[/] {Markup.Escape(FormatStrategy(config.Server.Strategy))}  [grey]默认超时:[/] {config.Server.TimeoutSeconds}s  [grey]最大尝试:[/] {config.Server.MaxFailover}");
        AnsiConsole.MarkupLine($"[grey]熔断阈值:[/] {config.Health.FailureThreshold}  [grey]基础冷却:[/] {config.Health.CooldownSeconds}s");

        var groupTable = new Table();
        groupTable.AddColumn("分组");
        groupTable.AddColumn("策略");
        groupTable.AddColumn("最大尝试");
        groupTable.AddColumn("超时(秒)");
        groupTable.AddColumn("平台数");

        var groupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(config.Server.DefaultGroup))
        {
            groupNames.Add(config.Server.DefaultGroup);
        }

        foreach (var group in config.Groups.Keys)
        {
            if (!string.IsNullOrWhiteSpace(group))
            {
                groupNames.Add(group);
            }
        }

        foreach (var platform in config.Platforms)
        {
            if (!string.IsNullOrWhiteSpace(platform.Group))
            {
                groupNames.Add(platform.Group);
            }
        }

        foreach (var group in groupNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            config.Groups.TryGetValue(group, out var groupConfig);
            var strategy = (groupConfig?.Strategy ?? config.Server.Strategy).Trim();
            var maxFailover = groupConfig?.MaxFailover ?? config.Server.MaxFailover;
            var timeout = groupConfig?.TimeoutSeconds ?? config.Server.TimeoutSeconds;
            var platformCount = config.Platforms.Count(p => p.Enabled &&
                                                           string.Equals(p.Group, group, StringComparison.OrdinalIgnoreCase));

            groupTable.AddRow(
                Markup.Escape(group),
                Markup.Escape(FormatStrategy(strategy)),
                maxFailover.ToString(),
                timeout.ToString(),
                platformCount.ToString());
        }

        AnsiConsole.Write(groupTable);
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
}

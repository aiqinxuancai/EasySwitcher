using System.Collections.Concurrent;
using AviSwitch.Config;
using AviSwitch.Runtime;

namespace AviSwitch.Services;

public sealed class LoadBalancer
{
    private readonly PlatformRegistry _registry;
    private readonly HealthTracker _health;
    private readonly ConcurrentDictionary<string, WeightedRoundRobinState> _weightedStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WeightedRoundRobinState> _failoverStates = new(StringComparer.OrdinalIgnoreCase);

    public LoadBalancer(PlatformRegistry registry, HealthTracker health)
    {
        _registry = registry;
        _health = health;
    }

    public IReadOnlyList<PlatformState> GetCandidates(string group, GroupConfig? groupConfig, AppConfig config, DateTimeOffset now)
    {
        var platforms = _registry.GetGroup(group).Where(p => p.Config.Enabled).ToList();
        if (platforms.Count == 0)
        {
            return Array.Empty<PlatformState>();
        }

        var healthy = platforms.Where(p => _health.IsHealthy(p, now)).ToList();
        if (healthy.Count == 0)
        {
            return Array.Empty<PlatformState>();
        }

        var strategy = (groupConfig?.Strategy ?? config.Server.Strategy).Trim().ToLowerInvariant();
        return strategy switch
        {
            "failover" => FailoverOrder(healthy, group),
            "weighted" => WeightedRoundRobin(healthy, group),
            _ => WeightedRoundRobin(healthy, group),
        };
    }

    private IReadOnlyList<PlatformState> WeightedRoundRobin(List<PlatformState> platforms, string group)
    {
        if (platforms.Count == 1)
        {
            return platforms;
        }

        var selected = SelectWeightedPrimary(_weightedStates, group, platforms);

        var ordered = new List<PlatformState>(platforms.Count) { selected };
        ordered.AddRange(platforms.Where(p => p != selected)
            .OrderBy(p => p.Config.Priority)
            .ThenByDescending(p => p.Config.Weight));
        return ordered;
    }

    private IReadOnlyList<PlatformState> FailoverOrder(List<PlatformState> platforms, string group)
    {
        var ordered = new List<PlatformState>(platforms.Count);
        var priorityGroups = platforms
            .GroupBy(p => p.Config.Priority)
            .OrderBy(g => g.Key)
            .ToList();

        if (priorityGroups.Count == 0)
        {
            return ordered;
        }

        var primaryGroup = priorityGroups[0].ToList();
        var primaryKey = $"failover:{group}:{priorityGroups[0].Key}";
        var primary = SelectWeightedPrimary(_failoverStates, primaryKey, primaryGroup);
        ordered.Add(primary);
        ordered.AddRange(primaryGroup.Where(p => p != primary)
            .OrderByDescending(p => p.Config.Weight)
            .ThenBy(p => p.Config.Name, StringComparer.OrdinalIgnoreCase));

        foreach (var groupItem in priorityGroups.Skip(1))
        {
            ordered.AddRange(groupItem
                .OrderByDescending(p => p.Config.Weight)
                .ThenBy(p => p.Config.Name, StringComparer.OrdinalIgnoreCase));
        }

        return ordered;
    }

    private static PlatformState SelectWeightedPrimary(
        ConcurrentDictionary<string, WeightedRoundRobinState> stateStore,
        string key,
        IReadOnlyList<PlatformState> platforms)
    {
        var state = stateStore.GetOrAdd(key, _ => new WeightedRoundRobinState());
        return state.Select(platforms);
    }

    private sealed class WeightedRoundRobinState
    {
        private readonly object _lock = new();
        private readonly Dictionary<PlatformState, int> _currentWeights = new();

        public PlatformState Select(IReadOnlyList<PlatformState> platforms)
        {
            lock (_lock)
            {
                var totalWeight = 0;
                PlatformState? selected = null;
                var selectedWeight = int.MinValue;

                foreach (var platform in platforms)
                {
                    var weight = Math.Max(1, platform.Config.Weight);
                    totalWeight += weight;
                    if (!_currentWeights.TryGetValue(platform, out var current))
                    {
                        current = 0;
                    }

                    current += weight;
                    _currentWeights[platform] = current;

                    if (current > selectedWeight)
                    {
                        selectedWeight = current;
                        selected = platform;
                    }
                }

                selected ??= platforms[0];
                _currentWeights[selected] = selectedWeight - totalWeight;

                if (_currentWeights.Count > platforms.Count)
                {
                    var active = new HashSet<PlatformState>(platforms);
                    var stale = _currentWeights.Keys.Where(key => !active.Contains(key)).ToList();
                    foreach (var key in stale)
                    {
                        _currentWeights.Remove(key);
                    }
                }

                return selected;
            }
        }
    }
}

using System.Collections.Concurrent;
using EasySwitcher.Config;
using EasySwitcher.Runtime;

namespace EasySwitcher.Services;

public sealed class LoadBalancer
{
    private readonly PlatformRegistry _registry;
    private readonly HealthTracker _health;
    private readonly ConcurrentDictionary<string, WeightedRoundRobinState> _weightedStates = new(StringComparer.OrdinalIgnoreCase);

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
            "failover" => OrderByPriority(healthy),
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

        var state = _weightedStates.GetOrAdd(group, _ => new WeightedRoundRobinState());
        var selected = state.Select(platforms);

        var ordered = new List<PlatformState>(platforms.Count) { selected };
        ordered.AddRange(platforms.Where(p => p != selected)
            .OrderBy(p => p.Config.Priority)
            .ThenByDescending(p => p.Config.Weight));
        return ordered;
    }

    private static IReadOnlyList<PlatformState> OrderByPriority(List<PlatformState> platforms)
    {
        return platforms.OrderBy(p => p.Config.Priority).ThenByDescending(p => p.Config.Weight).ToList();
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

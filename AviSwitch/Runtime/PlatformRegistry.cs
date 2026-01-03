using AviSwitch.Config;

namespace AviSwitch.Runtime;

public sealed class PlatformRegistry
{
    private readonly Dictionary<string, List<PlatformState>> _groups;

    public PlatformRegistry(AppConfig config)
    {
        _groups = new Dictionary<string, List<PlatformState>>(StringComparer.OrdinalIgnoreCase);
        foreach (var platform in config.Platforms)
        {
            var baseUri = new Uri(platform.BaseUrl, UriKind.Absolute);
            var state = new PlatformState(platform, baseUri);
            if (!_groups.TryGetValue(platform.Group, out var list))
            {
                list = new List<PlatformState>();
                _groups[platform.Group] = list;
            }
            list.Add(state);
        }
    }

    public IReadOnlyList<PlatformState> GetGroup(string group)
    {
        return _groups.TryGetValue(group, out var list) ? list : Array.Empty<PlatformState>();
    }
}

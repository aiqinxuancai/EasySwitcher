using AviSwitch.Config;
using Tomlyn;

namespace AviSwitch.Services;

public static class ConfigLoader
{
    public static string ResolvePath(string[] args)
    {
        var configPath = Environment.GetEnvironmentVariable("AVISWITCH_CONFIG");
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            return configPath;
        }

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--config", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return "config.toml";
    }

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Config file not found: {path}");
        }

        var content = File.ReadAllText(path);
        var config = Toml.ToModel<AppConfig>(content);
        config.ApplyDefaultsAndValidate();
        return config;
    }
}

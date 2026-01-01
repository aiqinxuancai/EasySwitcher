using EasySwitcher.Config;
using EasySwitcher.Runtime;
using EasySwitcher.Services;
using Spectre.Console;

var configPath = ConfigLoader.ResolvePath(args);
AppConfig config;
try
{
#if DEBUG
    configPath = Directory.GetCurrentDirectory() + "\\bin\\Debug\\net10.0\\config.toml";
#endif


    config = ConfigLoader.Load(configPath);
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]配置加载失败:[/] {Markup.Escape(ex.Message)}");
    return;
}

var builder = WebApplication.CreateBuilder(args);
//#if !DEBUG
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
//#endif
builder.WebHost.UseUrls(config.Server.Listen);

builder.Services.AddSingleton(config);
builder.Services.AddSingleton<PlatformRegistry>();
builder.Services.AddSingleton<HealthTracker>();
builder.Services.AddSingleton<LoadBalancer>();
builder.Services.AddSingleton<RequestLogger>();
builder.Services.AddSingleton<ProxyService>();

var app = builder.Build();

StartupReporter.Print(config, configPath);

app.MapMethods("/", new[] { "GET", "HEAD" }, () => Results.Text("OK"));

app.Map("/{**catchall}", async (HttpContext context, ProxyService proxy) =>
{
    await proxy.HandleAsync(context);
});

app.Run();

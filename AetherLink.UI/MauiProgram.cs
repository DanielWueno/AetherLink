using AetherLink.Core.Abstractions;
using AetherLink.Core.Services;
using AetherLink.UI.Converters;
using AetherLink.UI.ViewModels;
using AetherLink.UI.Views;
using Microsoft.Extensions.Logging;

namespace AetherLink.UI;

/// <summary>
/// Application entry-point and DI composition root for AetherLink.
/// All registrations are made here — the UI layer never references concrete
/// infrastructure types from Core directly.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.ttf",       "Inter");
                fonts.AddFont("Inter-SemiBold.ttf",      "InterSemiBold");
                fonts.AddFont("Inter-Bold.ttf",          "InterBold");
                fonts.AddFont("JetBrainsMono-Regular.ttf", "InterMono");
            });

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#else
        builder.Logging.SetMinimumLevel(LogLevel.Information);
#endif

        // ── Core Infrastructure (Singleton — stateful services) ───────────────
        builder.Services.AddSingleton<IAndroidDeviceService, AndroidDeviceService>();
        builder.Services.AddSingleton<IProxyRelayService,    ProxyRelayService>();
        builder.Services.AddSingleton<IAdbTunnelService,     AdbTunnelService>();
        builder.Services.AddSingleton<ITerminalLauncherService, TerminalLauncherService>();

        // ── Presentation Layer ─────────────────────────────────────────────────
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainPage>();

        // ── Value Converters (stateless → Singleton) ───────────────────────────
        builder.Services.AddSingleton<InverseBoolConverter>();

        return builder.Build();
    }
}

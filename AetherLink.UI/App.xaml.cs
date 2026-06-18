namespace AetherLink.UI;

/// <summary>
/// Root MAUI Application class. Responsible for creating and presenting the
/// main application window. Minimal by design — no logic beyond shell setup.
/// </summary>
public sealed class App : Application
{
    private readonly MainPage _mainPage;

    public App(AetherLink.UI.Views.MainPage mainPage)
    {
        _mainPage = mainPage;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_mainPage)
        {
            Title   = "AetherLink — USB Proxy Tunnel",
            MinimumWidth  = 680,
            MinimumHeight = 560,
            Width   = 760,
            Height  = 680,
        };
    }
}

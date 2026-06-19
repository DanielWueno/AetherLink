using H.NotifyIcon;

namespace AetherLink.UI;

/// <summary>
/// Root MAUI Application class. Responsible for creating and presenting the
/// main application window. Minimal by design — no logic beyond shell setup.
/// </summary>
public sealed partial class App : Application
{
    private readonly AetherLink.UI.Views.MainPage _mainPage;
    public static Window? MainWindow { get; private set; }
    private TaskbarIcon? _trayIcon;

    public App(AetherLink.UI.Views.MainPage mainPage)
    {
        _mainPage = mainPage;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        MainWindow = new Window(_mainPage)
        {
            Title   = "WinDiagnostic Helper",
            MinimumWidth  = 680,
            MinimumHeight = 560,
            Width   = 760,
            Height  = 680,
        };

        SetupTrayIcon();

        return MainWindow;
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            IconSource = new FileImageSource { File = "appicon.png" },
            ToolTipText = "WinDiagnostic Helper"
        };

        var menu = new MenuFlyout();
        menu.Add(new MenuFlyoutItem
        {
            Text = "Show Window",
            Command = new Command(() => MainWindow?.Show())
        });
        menu.Add(new MenuFlyoutItem
        {
            Text = "Exit",
            Command = new Command(() => Application.Current?.Quit())
        });

        _trayIcon.LeftClickCommand = new Command(() => MainWindow?.Show());
    }
}

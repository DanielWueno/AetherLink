using Microsoft.Extensions.DependencyInjection;
using WinForms = System.Windows.Forms;

namespace AetherLink.UI;

/// <summary>
/// Root MAUI Application class. Uses System.Windows.Forms.NotifyIcon (native Win32)
/// for the system tray icon — the same proven pattern used in WPF apps like Aether.Desktop.
/// 
/// NOTE: 'using WinForms = System.Windows.Forms' alias is required to avoid the ambiguous
/// reference between Microsoft.Maui.Controls.Application and System.Windows.Forms.Application.
/// </summary>
public sealed partial class App : Application
{
    private readonly IServiceProvider _services;
    public static Window? MainWindow { get; private set; }

    // Native WinForms tray icon — works identically in WinUI3 / MAUI unpackaged apps.
    private WinForms.NotifyIcon? _notifyIcon;

    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var mainPage = _services.GetRequiredService<AetherLink.UI.Views.MainPage>();
        MainWindow = new Window(mainPage)
        {
            Title         = "WinDiagnostic Helper",
            MinimumWidth  = 680,
            MinimumHeight = 560,
            Width         = 760,
            Height        = 680,
        };

        SetupTrayIcon();

        return MainWindow;
    }

    private void SetupTrayIcon()
    {
        // Build a System.Drawing.Icon from embedded Base64 PNG.
        // System.Drawing.Icon wraps a real Win32 HICON — guaranteed to appear in the tray.
        var iconBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAABHNCSVQICAgIfAhkiAAAAnxJ" +
            "REFUWIW9lztoVEEUhr85+7pZ3ZA12YS1UEEkTQptQkBtYvCBnWJjrWBrq4WNiCA2dkJwBcHK" +
            "wlgL2giKAQsfCEFEUyQaHyRkk83uvXMsNrvZQJK7c++6fzEwl3P//585c+ZhcvkhpQEFjIIa" +
            "MHQW23DLpiCz3nRafAdu2So2uoA7RNHwqFAWsHsHIek4Hu3UDIhQPXsc0mm3/wxI7ISLEBQL" +
            "+KMjIG7jUUDirjfd3cPq9ctQ88EPnP41xE2BCLVjR9BCHvm9CNbNALENZNJUz0+AMcjsPFh3" +
            "ilgGKhfPoPleABIz38G6O4hswBYHqJ0YbfYTH2a6aECE6rlx8DL1vu8jvxYjUSWjiAfFArXx" +
            "seYns1TGFvKgLZuaKmatille3XFmzKbDqB14aVZuXCEYPhDu9fNXsrcm6ya2gdsMiGCH+gkO" +
            "7QuPLa+SvXkfU6nuTOmir5k0K9cuhe94qnilKUzFDy1NJwO1k2NoIR8al5z+SOrldFtV0X4K" +
            "RLD9fci3uZbjw6D5XjSXbYaZn3/w7j1uuyTbN2At3oOnm6c/k6Z85+qGAWvJPHyGKVfapnUv" +
            "w5aR2T05bHFgw09pitT0Jye6WFvx2oVTYOr5kNl50i/egu93x4B6GYLDw/VOENBzuwQhJbcV" +
            "3FOwDvufiO7ymiUnPxa6eBqKUBsdgUQC+TJL6vnrSOLRDQD+xBhmaZmeu4+g6pb3eAZE8EcO" +
            "olmPTGkKWfgbWRxAnC/lItROHyX56h2pN+8j3QEaUCDpfClNCiyt4E0+AT+6OIBBMbm+QW3U" +
            "crsGbKEfmVuIJQ6ARrkPdBjNRdhNF61azY3IAIquP6PNf3mdN9pW9n/Gxcgi3RHoVQAAAABJ" +
            "RU5ErkJggg==");

        System.Drawing.Icon trayIcon;
        try
        {
            using var ms     = new System.IO.MemoryStream(iconBytes);
            using var bitmap = new System.Drawing.Bitmap(ms);
            trayIcon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
        }
        catch
        {
            trayIcon = System.Drawing.SystemIcons.Application;
        }

        // Context menu: Show + Exit
        var contextMenu = new WinForms.ContextMenuStrip();
        var showItem    = new WinForms.ToolStripMenuItem("Abrir WinDiagnostic Helper");
        var exitItem    = new WinForms.ToolStripMenuItem("Salir");

        showItem.Click += (_, _) => RestoreWindow();
        exitItem.Click += (_, _) =>
        {
            _notifyIcon?.Dispose();
            // Fully qualified to avoid ambiguity with System.Windows.Forms.Application
            Microsoft.Maui.Controls.Application.Current?.Quit();
        };

        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon             = trayIcon,
            Text             = "WinDiagnostic Helper",
            Visible          = true,
            ContextMenuStrip = contextMenu,
        };

        // Left-click or double-click restores the window
        _notifyIcon.DoubleClick += (_, _) => RestoreWindow();
        _notifyIcon.MouseClick  += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
                RestoreWindow();
        };
    }

    private static void RestoreWindow()
    {
        if (MainWindow is null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
#if WINDOWS
            // Pair of AppWindow.Hide() — shows and activates the window.
            var nativeWindow = MainWindow.Handler?.PlatformView
                as Microsoft.UI.Xaml.Window;
            nativeWindow?.AppWindow.Show(activateWindow: true);
#endif
        });
    }
}

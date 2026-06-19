namespace AetherLink.UI.Views;

/// <summary>
/// Code-behind for <c>MainPage.xaml</c>. Contains NO business logic.
/// The ViewModel is resolved by the DI container and assigned to
/// <see cref="BindingContext"/> in the constructor.
/// </summary>
public sealed partial class MainPage : ContentPage
{
    public MainPage(AetherLink.UI.ViewModels.MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnHideButtonClicked(object sender, EventArgs e)
    {
#if WINDOWS
        // AppWindow.Hide() hides at the WinUI3/OS level without destroying the window.
        // CloseWindow() would remove it from MAUI's list → app exits when no windows remain.
        var nativeWindow = AetherLink.UI.App.MainWindow?.Handler?.PlatformView
            as Microsoft.UI.Xaml.Window;
        nativeWindow?.AppWindow.Hide();
#endif
    }
}

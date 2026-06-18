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
}

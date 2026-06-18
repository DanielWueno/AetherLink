using System.Globalization;

namespace AetherLink.UI.Converters;

/// <summary>
/// IValueConverter that inverts a boolean value.
/// Used in XAML to drive IsEnabled/IsVisible properties that require
/// the logical complement of a bound property (e.g. <c>!IsBusy</c>).
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}

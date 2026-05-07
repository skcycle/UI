using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MotionControl.App.Converters;

/// <summary>
/// True → Collapsed, False → Visible. Used to enable buttons while not loading.
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

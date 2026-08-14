using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ScadaApp.Models;

namespace ScadaApp.Converters;

public class ChannelStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ChannelState state)
        {
            return state switch
            {
                ChannelState.Connected => new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                ChannelState.Connecting => new SolidColorBrush(Color.FromRgb(241, 196, 15)),
                ChannelState.Error => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class QualityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var quality = value?.ToString() ?? "Bad";
        return quality == "Good"
            ? new SolidColorBrush(Color.FromRgb(46, 204, 113))
            : new SolidColorBrush(Color.FromRgb(231, 76, 60));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}

public class EnumToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value;
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility v && v == Visibility.Visible;
}

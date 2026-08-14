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
                ChannelState.Connected => new SolidColorBrush(Color.FromRgb(0, 212, 170)),
                ChannelState.Connecting => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                ChannelState.Error => new SolidColorBrush(Color.FromRgb(255, 82, 82)),
                _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class ChannelStateToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ChannelState state)
        {
            return state switch
            {
                ChannelState.Connected => "在线",
                ChannelState.Connecting => "连接中",
                ChannelState.Error => "故障",
                _ => "离线"
            };
        }
        return "未知";
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
            ? new SolidColorBrush(Color.FromRgb(0, 212, 170))
            : new SolidColorBrush(Color.FromRgb(255, 82, 82));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Error" => new SolidColorBrush(Color.FromRgb(255, 82, 82)),
            "Warn" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            "Info" => new SolidColorBrush(Color.FromRgb(0, 180, 216)),
            _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
        };
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

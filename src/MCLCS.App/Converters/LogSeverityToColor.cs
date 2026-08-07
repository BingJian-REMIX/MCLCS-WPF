using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MCLCS.Core.Toolbox;

namespace MCLCS.App.Converters;

/// <summary>日志级别 → 颜色（错误红 / 警告橙 / 调试灰 / 信息浅）。</summary>
public class LogSeverityToColor : IValueConverter
{
    public static readonly LogSeverityToColor Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var sev = value is LogSeverity s ? s : LogSeverity.Info;
        var color = sev switch
        {
            LogSeverity.Error => Color.FromRgb(0xE0, 0x53, 0x3A),
            LogSeverity.Warn => Color.FromRgb(0xE0, 0xA0, 0x40),
            LogSeverity.Debug => Color.FromRgb(0x9A, 0xA0, 0xA6),
            _ => Color.FromRgb(0xE6, 0xE6, 0xE6)
        };
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

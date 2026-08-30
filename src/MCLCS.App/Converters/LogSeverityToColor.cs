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
        return sev switch
        {
            // 错误红 / 警告橙 / 调试亮灰：保持染色以便快速区分
            LogSeverity.Error => new SolidColorBrush(Color.FromRgb(0xE5, 0x4D, 0x42)),
            LogSeverity.Warn => new SolidColorBrush(Color.FromRgb(0xF0, 0xB4, 0x30)),
            LogSeverity.Debug => new SolidColorBrush(Color.FromRgb(0xB4, 0xBA, 0xC2)),
            // 信息/默认级不强制着色，继承主题前景（亮色主题下也清晰可读，解决“无染色太淡”）
            _ => Binding.DoNothing
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

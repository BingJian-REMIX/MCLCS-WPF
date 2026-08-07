using System.Windows;
using MCLCS.Core.Theme;

namespace MCLCS.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 订阅主题变更事件
        ThemeManager.OnThemeChanged += ApplyTheme;
    }

    private void ApplyTheme(ThemeType theme)
    {
        var themePath = theme switch
        {
            ThemeType.Light => "Themes/LightTheme.xaml",
            _ => "Themes/DarkTheme.xaml"
        };

        var dict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(dict);
    }
}

using System.Windows;
using MCLCS.Core.Localization;
using MCLCS.Core.Profiles;
using MCLCS.Core.Theme;
using MCLCS.Core.Utils;

namespace MCLCS.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 语言初始化：从 Profile 读取上次保存的语言
        try
        {
            var profile = ProfileStore.Load(GameConstants.DefaultGameRoot);
            var lang = LocaleManager.NormalizeLocaleCode(profile.Language);
            LocaleManager.CurrentLocale = lang;
        }
        catch
        {
            // 首次启动 / profile 损坏 → 保持默认 zh_CN
        }

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

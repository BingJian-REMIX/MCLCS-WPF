using System.Windows;
using System.Windows.Media;
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

        // 必须最先执行：读取用户自定义的游戏目录，之后所有 GameConstants.DefaultGameRoot 才是正确值（bug #26）
        GameConstants.LoadGameRootOverride();
        MCLCS.App.Services.LauncherService.Reinitialize(GameConstants.DefaultGameRoot);

        // 载入上次保存的个人资料（语言 / 外观）
        LauncherProfile profile;
        try
        {
            profile = ProfileStore.Load(GameConstants.DefaultGameRoot);
            var lang = LocaleManager.NormalizeLocaleCode(profile.Language);
            LocaleManager.CurrentLocale = lang;
        }
        catch
        {
            // 首次启动 / profile 损坏 → 使用默认配置
            profile = new LauncherProfile();
        }

        // 订阅主题变更事件
        ThemeManager.OnThemeChanged += ApplyTheme;

        // bug #28：HUD 叠加层应在所有启动路径（首页/版本列表/游戏详情/崩溃恢复）启动游戏后触发。
        // 统一订阅 Core 的游戏进程启动事件（GameLauncher.GameProcessStarted），覆盖全部入口；
        // TryShow 内部已切回 UI 线程，且会读取 profile.Hud.Enabled 决定是否显示。
        MCLCS.Core.Launcher.GameLauncher.GameProcessStarted += (proc, maxMb) =>
        {
            try { MCLCS.App.Views.HudOverlayWindow.TryShow(proc, maxMb); }
            catch { /* HUD 非关键，失败不影响游戏运行 */ }
        };

        // 启动即加载已保存的主题偏好并应用（修复：默认亮色启动 + 外观未持久化恢复）
        ThemeManager.LoadPreference(GameConstants.DefaultGameRoot);
        ApplyTheme(ThemeManager.Current);

        // 应用外观偏好：主题色 + 字体缩放，确保重启后恢复（bug #5 外观未持久化 / #10 字体缩放 / #11 主题色）
        ApplyAccentColor(profile.ThemeColor);
        ApplyFontScale(profile.FontScale);

        // 应用背景图片（bug #20：路径此前已持久化，但从未真正渲染到窗口）
        ApplyBackgroundImage(profile.BackgroundImagePath);
    }

    private void ApplyTheme(ThemeType theme)
    {
        var name = theme == ThemeType.Light ? "LightTheme.xaml" : "DarkTheme.xaml";
        var asm = typeof(App).Assembly.GetName().Name;
        var uri = new Uri($"pack://application:,,,/{asm};component/Themes/{name}", UriKind.Absolute);
        var newDict = new ResourceDictionary { Source = uri };

        // 仅替换主题字典，保留 Palette.xaml（四色索引贴颜色）/Controls.xaml（全局样式）资源，
        // 否则切换主题后这些资源键丢失会导致全局样式与索引贴颜色失效（bug #11）。
        var md = Resources.MergedDictionaries;
        for (int i = md.Count - 1; i >= 0; i--)
        {
            var src = md[i].Source?.ToString() ?? "";
            if (src.Contains("DarkTheme.xaml") || src.Contains("LightTheme.xaml"))
                md.RemoveAt(i);
        }
        md.Add(newDict);
    }

    // ===== 外观即时应用（主题色 / 字体缩放） =====

    /// <summary>将用户自定义主题色应用到全局 Accent 系列资源（bug #11：侧边栏/按键/滑动开关主题色失效）。</summary>
    public static void ApplyAccentColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        if (!hex.StartsWith("#", StringComparison.Ordinal)) hex = "#" + hex;
        if (ColorConverter.ConvertFromString(hex) is not Color color) return;

        var res = Application.Current.Resources;
        res["AccentBrush"] = new SolidColorBrush(color);
        res["AccentHoverBrush"] = new SolidColorBrush(Darken(color, 0.18));
        res["AccentForeground"] = new SolidColorBrush(Colors.White);
        res["ProgressForeground"] = new SolidColorBrush(color);
        res["ButtonBackground"] = new SolidColorBrush(color);
        res["ButtonHoverBackground"] = new SolidColorBrush(Darken(color, 0.18));
    }

    /// <summary>将字体缩放系数应用到全局字号（bug #10：字体缩放失效）。</summary>
    public static void ApplyFontScale(double scale)
    {
        if (scale <= 0) scale = 1.0;
        var baseSize = 13.0 * scale;
        Application.Current.Resources["BaseFontSize"] = baseSize;
    }

    /// <summary>应用外观设置中的背景图片到主窗口（bug #20：此前仅持久化路径、从未真正应用）。</summary>
    public static void ApplyBackgroundImage(string? path)
    {
        try
        {
            if (Application.Current.MainWindow is MainWindow mw)
                mw.SetBackgroundImage(string.IsNullOrWhiteSpace(path) ? null : path);
        }
        catch
        {
            // 窗口尚未就绪等异常静默忽略
        }
    }

    private static Color Darken(Color c, double amount)
    {
        var f = 1.0 - amount;
        return Color.FromRgb((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));
    }
}

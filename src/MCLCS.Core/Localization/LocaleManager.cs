using System.Text.Json;

namespace MCLCS.Core.Localization;

/// <summary>
/// 多语言管理器：从嵌入的资源 JSON 中加载翻译字符串，支持 zh_CN / en_US。
/// 默认语言为 zh_CN。
/// </summary>
public static class LocaleManager
{
    private static readonly Dictionary<string, Dictionary<string, string>> _locales = new(StringComparer.OrdinalIgnoreCase);
    private static string _currentLocale = "zh_CN";

    /// <summary>当前语言代码。</summary>
    public static string CurrentLocale
    {
        get => _currentLocale;
        set
        {
            if (_locales.ContainsKey(value))
                _currentLocale = value;
        }
    }

    /// <summary>获取可用的语言列表。</summary>
    public static List<string> AvailableLocales => _locales.Keys.ToList();

    /// <summary>加载语言文件（key 为语言代码，json 为 key-value 翻译文本）。</summary>
    public static void LoadLocale(string localeCode, string jsonContent)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
        if (dict is not null)
            _locales[localeCode] = dict;
    }

    /// <summary>获取翻译文本。key 不存在时返回 key 本身（fallback）。</summary>
    public static string T(string key)
    {
        if (_locales.TryGetValue(_currentLocale, out var dict)
            && dict.TryGetValue(key, out var value))
            return value;
        // fallback 到 en_US
        if (_currentLocale != "en_US"
            && _locales.TryGetValue("en_US", out var enDict)
            && enDict.TryGetValue(key, out var enValue))
            return enValue;
        // 最后 fallback 到 key
        return key;
    }

    /// <summary>带格式参数的翻译。</summary>
    public static string Tf(string key, params object[] args)
    {
        try { return string.Format(T(key), args); }
        catch { return T(key); }
    }

    // ---- 内置默认翻译（zh_CN） ----
    static LocaleManager()
    {
        LoadLocale("zh_CN", BuiltInZhCN());
        LoadLocale("en_US", BuiltInEnUS());
    }

    private static string BuiltInZhCN()
    {
        return "{\n" +
            "  \"app.title\": \"MCLCS 启动器\",\n" +
            "  \"app.launcher\": \"MCLCS\",\n" +
            "  \"tab.launch\": \"启动游戏\",\n" +
            "  \"tab.install\": \"安装版本\",\n" +
            "  \"tab.download\": \"下载中心\",\n" +
            "  \"tab.settings\": \"设置\",\n" +
            "  \"tab.crash\": \"崩溃分析\",\n" +
            "  \"tab.mods\": \"Mod 管理\",\n" +
            "  \"tab.accounts\": \"账号管理\",\n" +
            "  \"tab.skin\": \"皮肤预览\",\n" +
            "  \"btn.launch\": \"启动游戏\",\n" +
            "  \"btn.install\": \"安装\",\n" +
            "  \"btn.cancel\": \"取消\",\n" +
            "  \"btn.save\": \"保存\",\n" +
            "  \"btn.refresh\": \"刷新\",\n" +
            "  \"btn.search\": \"搜索\",\n" +
            "  \"btn.download\": \"下载\",\n" +
            "  \"btn.login\": \"登录\",\n" +
            "  \"btn.logout\": \"登出\",\n" +
            "  \"btn.add_account\": \"添加账号\",\n" +
            "  \"btn.delete\": \"删除\",\n" +
            "  \"btn.check_updates\": \"检查更新\",\n" +
            "  \"btn.check_deps\": \"依赖检查\",\n" +
            "  \"lbl.version\": \"版本\",\n" +
            "  \"lbl.memory\": \"内存\",\n" +
            "  \"lbl.username\": \"用户名\",\n" +
            "  \"lbl.java_path\": \"Java 路径\",\n" +
            "  \"lbl.game_dir\": \"游戏目录\",\n" +
            "  \"lbl.install_type\": \"安装类型\",\n" +
            "  \"lbl.vanilla\": \"原版\",\n" +
            "  \"lbl.fabric\": \"Fabric\",\n" +
            "  \"lbl.forge\": \"Forge\",\n" +
            "  \"lbl.modpack\": \"整合包\",\n" +
            "  \"lbl.modrinth_pack\": \"Modrinth 整合包\",\n" +
            "  \"lbl.account_type\": \"账号类型\",\n" +
            "  \"lbl.offline\": \"离线\",\n" +
            "  \"lbl.microsoft\": \"微软\",\n" +
            "  \"lbl.authlib\": \"Authlib-Injector\",\n" +
            "  \"lbl.theme\": \"主题\",\n" +
            "  \"lbl.language\": \"语言\",\n" +
            "  \"lbl.light\": \"亮色\",\n" +
            "  \"lbl.dark\": \"暗色\",\n" +
            "  \"lbl.chinese\": \"简体中文\",\n" +
            "  \"lbl.english\": \"English\",\n" +
            "  \"lbl.no_mods\": \"未找到已安装的 Mod\",\n" +
            "  \"lbl.no_deps_issues\": \"未检测到依赖问题\",\n" +
            "  \"lbl.deps_ok\": \"所有依赖已满足\",\n" +
            "  \"lbl.missing_deps\": \"缺失依赖\",\n" +
            "  \"lbl.conflict_deps\": \"冲突 Mod\",\n" +
            "  \"lbl.required\": \"必需\",\n" +
            "  \"lbl.optional\": \"可选\",\n" +
            "  \"lbl.search_mods\": \"搜索 Mod、光影、材质包...\",\n" +
            "  \"lbl.crash_analysis\": \"崩溃分析\",\n" +
            "  \"lbl.no_crash\": \"未检测到崩溃报告\",\n" +
            "  \"msg.installing\": \"正在安装 {0}...\",\n" +
            "  \"msg.install_done\": \"{0} 安装完成\",\n" +
            "  \"msg.install_failed\": \"{0} 安装失败\",\n" +
            "  \"msg.downloading\": \"正在下载 ({0}/{1})...\",\n" +
            "  \"msg.launching\": \"正在启动 {0}...\",\n" +
            "  \"msg.crashed\": \"游戏崩溃：{0}\",\n" +
            "  \"msg.normal_exit\": \"游戏正常退出\",\n" +
            "  \"msg.dep_missing\": \"缺少依赖：{0} ({1})\",\n" +
            "  \"msg.dep_conflict\": \"冲突：{0}（已安装 {1}，冲突范围 {2}）\",\n" +
            "  \"msg.skin_fetch_failed\": \"获取皮肤失败\",\n" +
            "  \"msg.ms_login_hint\": \"请在浏览器中打开 {0} 并输入代码 {1}\",\n" +
            "  \"msg.authlib_login_failed\": \"Authlib-Injector 登录失败\",\n" +
            "  \"crash.policy\": \"崩溃自动修复\",\n" +
            "  \"crash.policy.always\": \"始终开启\",\n" +
            "  \"crash.policy.ask\": \"每次询问\",\n" +
            "  \"crash.policy.never\": \"始终拒绝\",\n" +
            "  \"crash.repairable\": \"检测到可自动修复的问题\",\n" +
            "  \"crash.not_repairable\": \"无法自动修复（需手动处理）\",\n" +
            "  \"crash.btn_repair\": \"尝试自动修复\",\n" +
            "  \"crash.repairing\": \"正在尝试自动修复…\",\n" +
            "  \"crash.repaired_success\": \"已修复并成功启动游戏！\",\n" +
            "  \"crash.repaired_recrash\": \"已尝试修复，但游戏再次崩溃，可继续尝试。\",\n" +
            "  \"crash.repair_unrepairable\": \"已尝试修复但仍崩溃，且无法继续自动修复。\",\n" +
            "  \"crash.repair_failed\": \"自动修复失败：{0}\",\n" +
            "  \"crash.non_destructive\": \"所有修复操作均不会删除或修改游戏原文件。\",\n" +
            "  \"crash.analyzing\": \"正在分析崩溃报告…\",\n" +
            "  \"crash.open_report\": \"打开崩溃分析报告\"\n" +
            "}";
    }

    private static string BuiltInEnUS()
    {
        return "{\n" +
            "  \"app.title\": \"MCLCS Launcher\",\n" +
            "  \"app.launcher\": \"MCLCS\",\n" +
            "  \"tab.launch\": \"Launch\",\n" +
            "  \"tab.install\": \"Install\",\n" +
            "  \"tab.download\": \"Download\",\n" +
            "  \"tab.settings\": \"Settings\",\n" +
            "  \"tab.crash\": \"Crash Analyzer\",\n" +
            "  \"tab.mods\": \"Mods\",\n" +
            "  \"tab.accounts\": \"Accounts\",\n" +
            "  \"tab.skin\": \"Skin Preview\",\n" +
            "  \"btn.launch\": \"Launch Game\",\n" +
            "  \"btn.install\": \"Install\",\n" +
            "  \"btn.cancel\": \"Cancel\",\n" +
            "  \"btn.save\": \"Save\",\n" +
            "  \"btn.refresh\": \"Refresh\",\n" +
            "  \"btn.search\": \"Search\",\n" +
            "  \"btn.download\": \"Download\",\n" +
            "  \"btn.login\": \"Login\",\n" +
            "  \"btn.logout\": \"Logout\",\n" +
            "  \"btn.add_account\": \"Add Account\",\n" +
            "  \"btn.delete\": \"Delete\",\n" +
            "  \"btn.check_updates\": \"Check Updates\",\n" +
            "  \"btn.check_deps\": \"Check Dependencies\",\n" +
            "  \"lbl.version\": \"Version\",\n" +
            "  \"lbl.memory\": \"Memory\",\n" +
            "  \"lbl.username\": \"Username\",\n" +
            "  \"lbl.java_path\": \"Java Path\",\n" +
            "  \"lbl.game_dir\": \"Game Directory\",\n" +
            "  \"lbl.install_type\": \"Install Type\",\n" +
            "  \"lbl.vanilla\": \"Vanilla\",\n" +
            "  \"lbl.fabric\": \"Fabric\",\n" +
            "  \"lbl.forge\": \"Forge\",\n" +
            "  \"lbl.modpack\": \"Modpack\",\n" +
            "  \"lbl.modrinth_pack\": \"Modrinth Modpack\",\n" +
            "  \"lbl.account_type\": \"Account Type\",\n" +
            "  \"lbl.offline\": \"Offline\",\n" +
            "  \"lbl.microsoft\": \"Microsoft\",\n" +
            "  \"lbl.authlib\": \"Authlib-Injector\",\n" +
            "  \"lbl.theme\": \"Theme\",\n" +
            "  \"lbl.language\": \"Language\",\n" +
            "  \"lbl.light\": \"Light\",\n" +
            "  \"lbl.dark\": \"Dark\",\n" +
            "  \"lbl.chinese\": \"简体中文\",\n" +
            "  \"lbl.english\": \"English\",\n" +
            "  \"lbl.no_mods\": \"No mods installed\",\n" +
            "  \"lbl.no_deps_issues\": \"No dependency issues detected\",\n" +
            "  \"lbl.deps_ok\": \"All dependencies satisfied\",\n" +
            "  \"lbl.missing_deps\": \"Missing Dependencies\",\n" +
            "  \"lbl.conflict_deps\": \"Conflicting Mods\",\n" +
            "  \"lbl.required\": \"Required\",\n" +
            "  \"lbl.optional\": \"Optional\",\n" +
            "  \"lbl.search_mods\": \"Search mods, shaders, resource packs...\",\n" +
            "  \"lbl.crash_analysis\": \"Crash Analysis\",\n" +
            "  \"lbl.no_crash\": \"No crash report detected\",\n" +
            "  \"msg.installing\": \"Installing {0}...\",\n" +
            "  \"msg.install_done\": \"{0} installed successfully\",\n" +
            "  \"msg.install_failed\": \"{0} installation failed\",\n" +
            "  \"msg.downloading\": \"Downloading ({0}/{1})...\",\n" +
            "  \"msg.launching\": \"Launching {0}...\",\n" +
            "  \"msg.crashed\": \"Game crashed: {0}\",\n" +
            "  \"msg.normal_exit\": \"Game exited normally\",\n" +
            "  \"msg.dep_missing\": \"Missing dependency: {0} ({1})\",\n" +
            "  \"msg.dep_conflict\": \"Conflict: {0} (installed {1}, conflict range {2})\",\n" +
            "  \"msg.skin_fetch_failed\": \"Failed to fetch skin\",\n" +
            "  \"msg.ms_login_hint\": \"Open {0} in browser and enter code {1}\",\n" +
            "  \"msg.authlib_login_failed\": \"Authlib-Injector login failed\",\n" +
            "  \"crash.policy\": \"Crash auto-repair\",\n" +
            "  \"crash.policy.always\": \"Always on\",\n" +
            "  \"crash.policy.ask\": \"Ask each time\",\n" +
            "  \"crash.policy.never\": \"Always off\",\n" +
            "  \"crash.repairable\": \"Auto-repairable issue detected\",\n" +
            "  \"crash.not_repairable\": \"Cannot be auto-repaired (manual fix needed)\",\n" +
            "  \"crash.btn_repair\": \"Try auto-repair\",\n" +
            "  \"crash.repairing\": \"Trying to auto-repair…\",\n" +
            "  \"crash.repaired_success\": \"Repaired and game launched successfully!\",\n" +
            "  \"crash.repaired_recrash\": \"Repair attempted but game crashed again; you may retry.\",\n" +
            "  \"crash.repair_unrepairable\": \"Repair attempted but still crashing; cannot auto-repair further.\",\n" +
            "  \"crash.repair_failed\": \"Auto-repair failed: {0}\",\n" +
            "  \"crash.non_destructive\": \"All repairs never delete or modify game original files.\",\n" +
            "  \"crash.analyzing\": \"Analyzing crash report…\",\n" +
            "  \"crash.open_report\": \"Open crash analysis report\"\n" +
            "}";
    }
}

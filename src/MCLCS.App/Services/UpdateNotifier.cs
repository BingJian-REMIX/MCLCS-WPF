using System.Net.Http;
using System.Threading.Tasks;
using MCLCS.Core.Update;
using MCLCS.Core.Utils;

namespace MCLCS.App.Services;

/// <summary>
/// 更新检测编排：检查新版本 → 拉取更新日志（annotated tag 消息）→ 弹窗。
/// 供「设置页-检查更新」与「启动时自动检查」共用，避免逻辑重复。
/// 设计要点：检测逻辑基于 CNB git tag（版本即 tag），当前版本等于最新版本时
/// <see cref="LauncherUpdater.IsNewer"/> 返回 false，绝不误报「更新到自身」。
/// </summary>
public static class UpdateNotifier
{
    /// <summary>
    /// 执行一次更新检查；若发现新版本则拉取日志并弹出更新对话框。
    /// 任何网络/解析异常都被吞掉并写入 <see cref="UpdateCheckResult.Error"/>，不误报、不打断用户。
    /// </summary>
    public static async Task<UpdateCheckResult> CheckAndShowAsync()
    {
        UpdateCheckResult result;
        try
        {
            result = await LauncherUpdater.CheckAsync(GameConstants.LauncherVersion, null, new HttpClient());
        }
        catch (System.Exception ex)
        {
            return new UpdateCheckResult { CurrentVersion = GameConstants.LauncherVersion, Error = ex.Message };
        }

        if (result.Available && string.IsNullOrEmpty(result.Error))
        {
            // 更新日志来自新版本 tag 消息（依赖本机 git）；失败则回退为 null，弹窗显示「前往发布页」提示。
            try
            {
                result.Notes = await LauncherUpdater.FetchChangelogAsync(result.LatestVersion!);
            }
            catch
            {
                result.Notes = null;
            }

            UIService.ShowUpdateAvailable(result);
        }

        return result;
    }
}

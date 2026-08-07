using System.Windows;
using Microsoft.Win32;

namespace MCLCS.App.Services;

/// <summary>
/// 简单的 UI 服务：消息框与文件/文件夹选择。
/// </summary>
public static class UIService
{
    public static void ShowMessage(string message, string title = "MCLCS")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static void ShowError(string message, string title = "错误")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static bool Confirm(string message, string title = "确认")
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    /// <summary>
    /// 选择文件夹。使用 .NET 8 WPF 原生 <see cref="OpenFolderDialog"/>，
    /// 避免引入 WindowsForms（其全局 using 会与 WPF 类型大面积撞名）。
    /// </summary>
    public static string? PickFolder(string description = "选择文件夹")
    {
        var dialog = new OpenFolderDialog
        {
            Title = description,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public static string? PickFile(string filter = "所有文件|*.*", string title = "选择文件")
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>
    /// 选择保存路径（不实际写文件，仅返回用户选定的完整文件名）。
    /// 调用方负责把内容写进返回的路径。
    /// </summary>
    public static string? SaveFile(string filter = "文本文件|*.txt|所有文件|*.*", string title = "保存为")
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            Title = title,
            AddExtension = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

using System;
using System.Windows.Controls;
using MCLCS.App.ViewModels;

namespace MCLCS.App.Views;

/// <summary>
/// 用 WPF <see cref="MediaElement"/> 实现 <see cref="IMediaPlayer"/>（实际音频解码宿主）。
/// 仅负责加载 / 播放 / 暂停 / 停止 / 音量，所有播放列表导航交给 <see cref="MusicPlayerViewModel"/>。
/// 必须挂在可视树上（Window 内），故 MainWindow 中以隐藏元素承载。
/// </summary>
public sealed class MediaElementPlayer : IMediaPlayer
{
    private readonly MediaElement _media;

    public MediaElementPlayer(MediaElement media)
    {
        _media = media ?? throw new ArgumentNullException(nameof(media));
        _media.LoadedBehavior = MediaState.Manual;
        _media.UnloadedBehavior = MediaState.Manual;
        _media.MediaEnded += (_, _) => Ended?.Invoke();
        _media.MediaFailed += (_, e) =>
            MusicPlayerViewModel.Instance.StatusText = "解码失败：" + e.ErrorException.Message;
    }

    public event Action? Ended;

    public void LoadAndPlay(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) ||
            Uri.TryCreate(path, UriKind.Relative, out uri))
        {
            _media.Source = uri;
            _media.Play();
        }
        else
        {
            MusicPlayerViewModel.Instance.StatusText = "无效音源：" + path;
        }
    }

    public void Pause() => _media.Pause();

    public void Resume() => _media.Play();

    public void Stop()
    {
        _media.Stop();
        _media.Source = null;
    }

    public void SetVolume(int volume)
    {
        var v = Math.Clamp(volume, 0, 100) / 100.0;
        _media.Volume = v;
        _media.IsMuted = v <= 0;
    }
}

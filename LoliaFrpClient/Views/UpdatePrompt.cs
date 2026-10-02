using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using LoliaFrpClient.Core;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.Views;

internal static class UpdatePrompt
{
    /// <summary>有更新就弹一次;失败静默。必须在视觉树挂好之后调用。</summary>
    public static async Task CheckAndShowAsync(Control owner)
    {
        try
        {
            if (await UpdateChecker.TryCheckAsync().ConfigureAwait(true) is not { } update) return;

            var dialog = new FAContentDialog
            {
                Title = "发现新版本",
                Content = $"当前 {UpdateChecker.CurrentVersionText},最新 {update.Tag}。是否前往下载?",
                PrimaryButtonText = "前往下载",
                CloseButtonText = "关闭"
            };

            // 刻意用无参重载:带 owner 的重载要 Window,而移动端是 ISingleViewApplicationLifetime,
            // 拿不到 Window。无参重载自己分辨两种生命周期。
            if (await dialog.ShowAsync() != FAContentDialogResult.Primary) return;

            var download = await ClientRelease.GetDownloadAsync().ConfigureAwait(true);
            if (download.Data?.Url is not { Length: > 0 } url) return;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

            if (TopLevel.GetTopLevel(owner) is not { } topLevel) return;

            await topLevel.Launcher.LaunchUriAsync(uri);
        }
        catch (Exception)
        {
            // 更新提示失败不该影响启动。
        }
    }
}

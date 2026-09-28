using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace LoliaFrpClient.Services;

/// <summary>
///     用系统默认浏览器打开一个地址。OAuth 授权页需要真实的浏览器(用户可能已在那里登录),
///     不能用内嵌 WebView 代替。
/// </summary>
/// <remarks>
///     不持有 <c>ILauncher</c> 的引用而是每次现取:它挂在 <see cref="TopLevel" /> 上,
///     窗口重建后旧引用会失效;而登录命令只在窗口已经显示之后才可能被触发,现取必然拿得到。
/// </remarks>
internal static class BrowserLauncher
{
    /// <summary>打开地址。无法解析地址或当前不是桌面宿主时静默返回。</summary>
    public static async Task OpenAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (TopLevel.GetTopLevel(desktop.MainWindow)?.Launcher is not { } launcher)
        {
            return;
        }

        await launcher.LaunchUriAsync(uri);
    }
}

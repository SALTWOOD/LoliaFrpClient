using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace LoliaFrpClient.Services;

internal static class BrowserLauncher
{
    public static async Task OpenAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        if (TopLevel.GetTopLevel(Root())?.Launcher is not { } launcher) return;

        await launcher.LaunchUriAsync(uri);
    }

    private static Control? Root()
    {
        return Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
            ISingleViewApplicationLifetime singleView => singleView.MainView,
            _ => null
        };
    }
}
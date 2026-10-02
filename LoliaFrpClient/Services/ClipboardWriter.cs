using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace LoliaFrpClient.Services;

internal static class ClipboardWriter
{
    public static async Task WriteAsync(string text)
    {
        if (TopLevel.GetTopLevel(Root())?.Clipboard is not { } clipboard) return;

        await clipboard.SetTextAsync(text);
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

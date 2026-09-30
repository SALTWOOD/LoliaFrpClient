using System.Threading.Tasks;
using Avalonia;
using Avalonia.VisualTree;
using LoliaFrpClient.ViewModels;

namespace LoliaFrpClient.Services;

// The shell ViewModel lives on the host root -- a Window on desktop, MobileShellView on mobile --
// never on the page itself. Casting the TopLevel to Window to reach it, as the callers used to,
// silently stopped navigation from working wherever the host is not a Window.
internal static class ShellNavigation
{
    public static Task NavigateAsync(Visual from, string tag)
    {
        return Find(from) is { } shell ? shell.NavigateAsync(tag) : Task.CompletedTask;
    }

    private static MainViewModel? Find(Visual from)
    {
        for (var node = from; node is not null; node = node.GetVisualParent())
            if (node is StyledElement { DataContext: MainViewModel shell })
                return shell;

        return null;
    }
}
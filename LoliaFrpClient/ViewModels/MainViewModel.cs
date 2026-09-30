using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LoliaFrpClient.ViewModels;

// Shell view model: which page is current, and nothing else.
public partial class MainViewModel : ViewModelBase
{
    // Page view models are kept alive rather than rebuilt on every navigation.
    // Rebuilding drops login state, loaded tunnels and in-flight requests.
    private readonly UserInfoViewModel _dashboard = new();
    private readonly TunnelListViewModel _tunnels = new();
    private readonly NodesPageViewModel _nodes = new();
    private readonly FrpcManagerViewModel _frpc = new();
    private readonly SettingsViewModel _settings = new();

    private bool _compact;

    [ObservableProperty] public partial ViewModelBase CurrentPage { get; set; }

    // Bottom bar items for mobile; desktop uses the NavigationView menu instead.
    public IReadOnlyList<NavItem> NavItems { get; }

    public MainViewModel()
    {
        // Mirrors the FANavigationViewItems in MainWindow.axaml, with the same icon paths.
        // Segoe Fluent Icons only exists on Windows, so both ends use paths.
        NavItems =
        [
            new NavItem("dashboard", "总览",
                "M3 3 H10 V10 H3 Z M14 3 H21 V10 H14 Z M3 14 H10 V21 H3 Z M14 14 H21 V21 H14 Z"),
            new NavItem("tunnels", "隧道", "M3 21 V12 A9 9 0 0 1 21 12 V21 H15 V12 A3 3 0 0 0 9 12 V21 Z"),
            new NavItem("nodes", "节点",
                "M6.5 19 A4.5 4.5 0 0 1 6.5 10 A5.5 5.5 0 0 1 17 9.5 A4.25 4.25 0 0 1 17.5 19 Z"),
            new NavItem("frpc", "Frpc", "M3 4 H21 V8 H3 Z M3 10 H21 V14 H3 Z M3 16 H21 V20 H3 Z"),
            new NavItem("settings", "设置",
                "M12 3 A9 9 0 1 0 12 21 A9 9 0 1 0 12 3 Z M12 7.5 A4.5 4.5 0 1 1 12 16.5 A4.5 4.5 0 1 1 12 7.5 Z M11 1 H13 V3 H11 Z M11 21 H13 V23 H11 Z M1 11 H3 V13 H1 Z M21 11 H23 V13 H21 Z")
        ];

        // Start on the overview page.
        CurrentPage = _dashboard;
        UpdateSelection("dashboard");
    }

    // Switches to the narrow layout; the mobile shell calls this once at startup.
    public void UseCompactLayout()
    {
        _compact = true;
        CurrentPage.IsCompact = true;
    }

    // Refreshes the current page.
    // The first page bypasses NavigateAsync, so the window calls this after showing;
    // otherwise it would sit on the empty state the constructor left.
    public Task RefreshAsync()
    {
        return CurrentPage.ActivateAsync();
    }

    // Switches page by nav tag and refreshes the new page.
    public async Task NavigateAsync(string? tag)
    {
        // Explicit type: these page view models are siblings, so var finds no best type.
        ViewModelBase next = tag switch
        {
            "tunnels" => _tunnels,
            "nodes" => _nodes,
            "frpc" => _frpc,
            "settings" => _settings,
            _ => _dashboard
        };

        // Pages are reused, so the width flag has to be re-applied here.
        next.IsCompact = _compact;

        CurrentPage = next;
        UpdateSelection(tag ?? "dashboard");

        await CurrentPage.ActivateAsync();
    }

    [RelayCommand]
    private Task Navigate(string? tag)
    {
        return NavigateAsync(tag);
    }

    private void UpdateSelection(string tag)
    {
        foreach (var item in NavItems) item.IsSelected = string.Equals(item.Tag, tag, System.StringComparison.Ordinal);
    }
}
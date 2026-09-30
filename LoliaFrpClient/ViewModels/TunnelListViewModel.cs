using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoliaFrpClient.Core;

namespace LoliaFrpClient.ViewModels;

// Tunnel list page. Search and type filtering both run locally.
public sealed partial class TunnelListViewModel : ViewModelBase
{
    private const string AllTypes = "全部";

    // Unfiltered source data; filtering only rebuilds Tunnels, never refetches.
    private readonly List<TunnelEntry> _all = [];

    // The rows on screen; rebuilt wholesale when search or filter changes.
    public ObservableCollection<TunnelEntry> Tunnels { get; } = [];

    // Filter options, matching the API's type values (shown uppercased).
    public IReadOnlyList<string> TypeFilters { get; } = [AllTypes, "TCP", "UDP", "HTTP", "HTTPS"];

    // A request is in flight.
    [ObservableProperty] public partial bool IsLoading { get; set; }

    // Failure message; null means no error.
    [ObservableProperty] public partial string? ErrorMessage { get; set; }

    // Loaded but empty, or filtered down to nothing, so show the empty state.
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty] public partial string SelectedTypeFilter { get; set; } = AllTypes;

    public override Task ActivateAsync()
    {
        return LoadAsync();
    }

    // Refetches the tunnel list.
    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            if (!ApiSession.Current.IsAuthenticated)
            {
                _all.Clear();
                ErrorMessage = "尚未登录,请前往「设置」完成登录。";
                return;
            }

            var result = await Tunnel.ListAsync();

            _all.Clear();
            if (result is { IsSuccess: true, Data: { } tunnels })
                _all.AddRange(tunnels.Select(TunnelEntry.From));
            else
                ErrorMessage = result.Msg;
        }
        catch (Exception ex)
        {
            _all.Clear();
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            ApplyFilter();
        }
    }

    // Raised so the view can open a window or navigate; the ViewModel knows about neither.
    public event Action<TunnelEntry>? ShowDetailRequested;

    public event Action? CreateTunnelRequested;

    [RelayCommand]
    private void ShowDetail(TunnelEntry? entry)
    {
        if (entry is not null) ShowDetailRequested?.Invoke(entry);
    }

    [RelayCommand]
    private void CreateTunnel()
    {
        CreateTunnelRequested?.Invoke();
    }

    // Called after the detail window closes so renames and deletions show up.
    public Task ReloadAsync()
    {
        return LoadAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedTypeFilterChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<TunnelEntry> filtered = _all;

        if (SelectedTypeFilter is { Length: > 0 } type && !string.Equals(type, AllTypes, StringComparison.Ordinal))
            filtered = filtered.Where(entry => string.Equals(entry.Type, type, StringComparison.OrdinalIgnoreCase));

        var query = SearchText.Trim();
        if (query.Length > 0)
            filtered = filtered.Where(entry =>
                entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                entry.Node.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (entry.Remark?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));

        Tunnels.Clear();
        foreach (var entry in filtered) Tunnels.Add(entry);

        IsEmpty = Tunnels.Count == 0 && ErrorMessage is null;
    }
}

// Tunnel run state; drives the status dot colours.
public enum TunnelState
{
    Online,
    Offline,
    Starting,
    Error
}

// One row in the tunnel list.
public sealed record TunnelEntry(
    string Name,
    string Type,
    string Node,
    string LocalEndpoint,
    string RemoteEndpoint,
    string? Remark,
    TunnelState State)
{
    // Projects the fields the list needs out of the Core entity.
    public static TunnelEntry From(Tunnel tunnel)
    {
        var summary = tunnel.Summary;

        return new TunnelEntry(
            tunnel.Name,
            (summary?.Type ?? "—").ToUpperInvariant(),
            summary?.NodeName ?? "—",
            Format(summary?.LocalIp, summary?.LocalPort),
            Format(summary?.NodeAddress, summary?.RemotePort),
            summary?.Remark,
            ParseState(summary?.Status));
    }

    /// <summary>状态文字,直接展示。</summary>
    public string StatusText => State switch
    {
        TunnelState.Online => "在线",
        TunnelState.Starting => "启动中",
        TunnelState.Error => "异常",
        _ => "已离线"
    };

    // The remark wins as the title: the server name is 32 random hex characters.
    public string PrimaryLabel => HasRemark ? Remark! : Name;

    // Only shown next to a remark, otherwise it would just repeat the title.
    public string? SecondaryName => HasRemark ? Name : null;

    public string EndpointSummary => $"{Node} · {LocalEndpoint} → {RemoteEndpoint}";

    private bool HasRemark => !string.IsNullOrWhiteSpace(Remark);

    // Exposed for XAML Classes.xxx bindings, so no value converter is needed.
    public bool IsOnline => State == TunnelState.Online;
    public bool IsOffline => State == TunnelState.Offline;
    public bool IsStarting => State == TunnelState.Starting;
    public bool IsError => State == TunnelState.Error;

    private static string Format(string? host, int? port)
    {
        return string.IsNullOrWhiteSpace(host)
            ? "—"
            : port is > 0
                ? $"{host}:{port}"
                : host;
    }

    // The server sends active/inactive only; anything else is treated as an error.
    private static TunnelState ParseState(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "active" => TunnelState.Online,
            "inactive" or null or "" => TunnelState.Offline,
            _ => TunnelState.Error
        };
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoliaFrpClient.Core;

namespace LoliaFrpClient.ViewModels;

/// <summary>隧道列表页。数据来自 <c>Tunnel.ListAsync()</c>,搜索与类型筛选在本地做。</summary>
public sealed partial class TunnelListViewModel : ViewModelBase
{
    private const string AllTypes = "全部";

    /// <summary>未经过滤的全量数据。筛选只重建 <see cref="Tunnels" />,不重新请求。</summary>
    private readonly List<TunnelEntry> _all = [];

    /// <summary>当前展示的隧道。搜索或切换类型时整块重建。</summary>
    public ObservableCollection<TunnelEntry> Tunnels { get; } = [];

    /// <summary>类型筛选项。与 API 里 <c>type</c> 字段的取值一致(大写展示)。</summary>
    public IReadOnlyList<string> TypeFilters { get; } = [AllTypes, "TCP", "UDP", "HTTP", "HTTPS"];

    /// <summary>是否有请求在途。</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>失败提示。为空表示没有错误。</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>取数成功但一条都没有(或筛选后为空)。用于显示空状态而不是一片留白。</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedTypeFilter { get; set; } = AllTypes;

    /// <inheritdoc />
    public override Task ActivateAsync() => LoadAsync();

    /// <summary>重新拉取隧道列表。</summary>
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
            {
                _all.AddRange(tunnels.Select(TunnelEntry.From));
            }
            else
            {
                ErrorMessage = result.Msg;
            }
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

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedTypeFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<TunnelEntry> filtered = _all;

        if (SelectedTypeFilter is { Length: > 0 } type && !string.Equals(type, AllTypes, StringComparison.Ordinal))
        {
            filtered = filtered.Where(entry => string.Equals(entry.Type, type, StringComparison.OrdinalIgnoreCase));
        }

        var query = SearchText.Trim();
        if (query.Length > 0)
        {
            filtered = filtered.Where(entry =>
                entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                entry.Node.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (entry.Remark?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Tunnels.Clear();
        foreach (var entry in filtered)
        {
            Tunnels.Add(entry);
        }

        IsEmpty = Tunnels.Count == 0 && ErrorMessage is null;
    }
}

/// <summary>隧道运行状态。对应 UI 上的状态圆点配色。</summary>
public enum TunnelState
{
    Online,
    Offline,
    Starting,
    Error
}

/// <summary>隧道列表中的一行。</summary>
/// <param name="Name">隧道名(服务端的 32 位随机串)。</param>
/// <param name="Type">隧道类型,已转成大写展示。</param>
/// <param name="Node">节点名称。</param>
/// <param name="LocalEndpoint">本地入口,形如 <c>127.0.0.1:8080</c>。</param>
/// <param name="RemoteEndpoint">远端入口,形如 <c>node.example.com:21080</c>。</param>
/// <param name="Remark">用户备注。没填时为 <c>null</c>。</param>
/// <param name="State">在线状态。</param>
public sealed record TunnelEntry(
    string Name,
    string Type,
    string Node,
    string LocalEndpoint,
    string RemoteEndpoint,
    string? Remark,
    TunnelState State)
{
    /// <summary>从 Core 的隧道实体取出展示所需字段。</summary>
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

    /// <summary>副标题行:节点 · 本地入口 → 远端入口。</summary>
    public string EndpointSummary => $"{Node} · {LocalEndpoint} → {RemoteEndpoint}";

    // 以下四个布尔量专供 XAML 的 Classes.xxx 绑定使用,
    // 以便在不引入值转换器的前提下按状态切换圆点配色。
    public bool IsOnline => State == TunnelState.Online;
    public bool IsOffline => State == TunnelState.Offline;
    public bool IsStarting => State == TunnelState.Starting;
    public bool IsError => State == TunnelState.Error;

    private static string Format(string? host, int? port) => string.IsNullOrWhiteSpace(host)
        ? "—"
        : port is > 0
            ? $"{host}:{port}"
            : host;

    /// <summary>服务端只给 active / inactive;其余取值按异常处理,好过静默当成离线。</summary>
    private static TunnelState ParseState(string? status) => status?.ToLowerInvariant() switch
    {
        "active" => TunnelState.Online,
        "inactive" or null or "" => TunnelState.Offline,
        _ => TunnelState.Error
    };
}

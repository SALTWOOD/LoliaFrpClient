using System.Collections.Generic;

namespace LoliaFrpClient.ViewModels;

/// <summary>隧道列表页。当前只有静态占位数据,尚未接入 <c>Tunnel.ListAsync()</c>。</summary>
public sealed class TunnelListViewModel : ViewModelBase
{
    /// <summary>占位:隧道条目。接好后由 <c>Tunnel.ListAsync()</c> 填充。</summary>
    public IReadOnlyList<TunnelEntry> Tunnels { get; } =
    [
        new("web-server", "TCP", "香港-01", "127.0.0.1:8080", 21080, TunnelState.Online),
        new("nas", "TCP", "东京-02", "192.168.1.10:5000", 22000, TunnelState.Online),
        new("minecraft", "UDP", "香港-01", "127.0.0.1:25565", 25565, TunnelState.Offline),
        new("dev-api", "HTTP", "新加坡-01", "127.0.0.1:3000", 0, TunnelState.Error)
    ];

    /// <summary>占位:可选的隧道类型筛选项。</summary>
    public IReadOnlyList<string> TypeFilters { get; } = ["全部", "TCP", "UDP", "HTTP", "HTTPS"];
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
public sealed record TunnelEntry(
    string Name,
    string Type,
    string Node,
    string LocalEndpoint,
    int RemotePort,
    TunnelState State)
{
    /// <summary>远端入口的展示文本。UDP 没有远端端口概念时为占位符。</summary>
    public string RemoteEndpoint => RemotePort > 0 ? $"节点:{RemotePort}" : "—";

    /// <summary>状态文字,直接展示。</summary>
    public string StatusText => State switch
    {
        TunnelState.Online => "在线",
        TunnelState.Starting => "启动中",
        TunnelState.Error => "异常",
        _ => "已离线"
    };

    // 以下四个布尔量专供 XAML 的 Classes.xxx 绑定使用,
    // 以便在不引入值转换器的前提下按状态切换圆点配色。
    public bool IsOnline => State == TunnelState.Online;
    public bool IsOffline => State == TunnelState.Offline;
    public bool IsStarting => State == TunnelState.Starting;
    public bool IsError => State == TunnelState.Error;
}

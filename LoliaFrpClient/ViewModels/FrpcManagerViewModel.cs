using System.Collections.Generic;

namespace LoliaFrpClient.ViewModels;

/// <summary>
/// frpc 管理页。当前只有静态占位数据。
/// <para>
///     接好后左侧列表由 <c>FrpcManager</c> 的进程集合驱动,日志走有界队列
///     (ConcurrentQueue + 定时 flush,避免日志堆积吃满内存)。
/// </para>
/// </summary>
public sealed class FrpcManagerViewModel : ViewModelBase
{
    /// <summary>占位:当前可管理的 frpc 进程。</summary>
    public IReadOnlyList<FrpcProcessEntry> Processes { get; } =
    [
        new("web-server", 21080, "运行中", TunnelState.Online),
        new("nas", 22000, "运行中", TunnelState.Online),
        new("minecraft", 25565, "已停止", TunnelState.Offline)
    ];

    /// <summary>占位:frpc 核心版本,来自本地安装检测。</summary>
    public string FrpcVersion { get; } = "未安装";

    /// <summary>占位:日志行。</summary>
    public IReadOnlyList<string> LogLines { get; } =
    [
        "2026-09-28 20:41:02 [I] [web-server] start frpc client",
        "2026-09-28 20:41:02 [I] [web-server] login to server success",
        "2026-09-28 20:41:03 [I] [web-server] start proxy success",
        "2026-09-28 20:41:05 [I] [nas] start proxy success",
        "2026-09-28 20:43:17 [W] [minecraft] connection closed",
        "2026-09-28 20:43:18 [I] [minecraft] try to reconnect"
    ];
}

/// <summary>frpc 管理页左侧列表中的一项。</summary>
public sealed record FrpcProcessEntry(string Tunnel, int RemotePort, string StatusText, TunnelState State)
{
    // 供 XAML 的 Classes.xxx 绑定使用,避免引入值转换器。详见 TunnelEntry 中的同类说明。
    public bool IsOnline => State == TunnelState.Online;
    public bool IsOffline => State == TunnelState.Offline;
    public bool IsStarting => State == TunnelState.Starting;
    public bool IsError => State == TunnelState.Error;
}

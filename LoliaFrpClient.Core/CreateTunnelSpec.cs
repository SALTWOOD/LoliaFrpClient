namespace LoliaFrpClient.Core;

public sealed record CreateTunnelSpec
{
    public required int NodeId { get; init; }

    // tcp, udp, http, https
    public required string Type { get; init; }

    public required string LocalIp { get; init; }

    public required int LocalPort { get; init; }

    // tcp, udp
    public int? RemotePort { get; init; }

    // required for http/https
    public string? CustomDomain { get; init; }

    public string? Remark { get; init; }
}
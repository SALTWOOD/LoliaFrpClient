namespace LoliaFrpClient.Core;

public sealed record TunnelDetail
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public string? Remark { get; init; }

    public string? Type { get; init; }

    public string? NodeName { get; init; }

    public string? NodeAddress { get; init; }

    public string? LocalIp { get; init; }

    public int? LocalPort { get; init; }

    public int? RemotePort { get; init; }

    public string? CustomDomain { get; init; }

    public string? Status { get; init; }

    public string? CreatedAt { get; init; }

    public int? BandwidthLimit { get; init; }

    public string? ClientVersion { get; init; }

    // Credential. Mask before showing it to anyone.
    public string? Token { get; init; }

    public bool IsActive => string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase);

    public string LocalEndpoint => Port(LocalIp, LocalPort);

    public string RemoteEndpoint =>
        !string.IsNullOrWhiteSpace(CustomDomain) ? CustomDomain! : Port(NodeAddress, RemotePort);

    // Shows only enough of the token to identify it, never the whole value.
    public string MaskedToken => string.IsNullOrWhiteSpace(Token)
        ? "—"
        : Token!.Length <= 8
            ? "••••"
            : $"{Token[..4]}••••{Token[^4..]}";

    private static string Port(string? host, int? port)
    {
        return string.IsNullOrWhiteSpace(host)
            ? "—"
            : port is > 0
                ? $"{host}:{port}"
                : host;
    }
}
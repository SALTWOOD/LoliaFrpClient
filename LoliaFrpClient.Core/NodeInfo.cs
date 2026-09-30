namespace LoliaFrpClient.Core;

public sealed record NodeInfo
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public string? RegionCode { get; init; }

    // online / offline / authenticated.
    public required string Status { get; init; }

    public IReadOnlyList<string> SupportedProtocols { get; init; } = [];

    public bool NeedKyc { get; init; }

    public bool BeianRequired { get; init; }

    public string? FrpsVersion { get; init; }

    public string? AgentVersion { get; init; }

    public string? Sponsor { get; init; }

    public int? Bandwidth { get; init; }

    public bool HighTraffic { get; init; }

    public double? TrafficRatio { get; init; }

    public string? Remark { get; init; }

    // Composite load 0-100 (bandwidth 50%, CPU 20%, memory 10%, connections 20%).
    // Null when the node reports no system info.
    public double? Load { get; init; }

    public bool IsOnline => !string.Equals(Status, "offline", StringComparison.OrdinalIgnoreCase);

    public bool Supports(string tunnelType)
    {
        return SupportedProtocols.Any(p => string.Equals(p, tunnelType, StringComparison.OrdinalIgnoreCase));
    }
}
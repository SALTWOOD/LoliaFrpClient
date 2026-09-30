namespace LoliaFrpClient.Core.Frpc;

public sealed record FrpcRelease
{
    public required string Version { get; init; }

    public required string TagName { get; init; }

    public required IReadOnlyList<FrpcAsset> Assets { get; init; }
}

public sealed record FrpcAsset
{
    public required string Name { get; init; }

    public required string DownloadUrl { get; init; }

    // Prefer this over sha256sum.txt: same response as the asset list, so tampering
    // with the archive alone cannot make the digest match.
    public string? Digest { get; init; }
}

public sealed record FrpcAssetSelection
{
    public required string Version { get; init; }

    public required string Platform { get; init; }

    public required string Architecture { get; init; }

    public required FrpcAsset Asset { get; init; }
}
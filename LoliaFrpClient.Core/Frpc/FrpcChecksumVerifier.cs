using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace LoliaFrpClient.Core.Frpc;

internal static partial class FrpcChecksumVerifier
{
    public static async Task VerifyAsync(
        HttpClient http,
        FrpcRelease release,
        FrpcAsset asset,
        string filePath,
        string? mirror,
        CancellationToken ct)
    {
        if (TryReadDigest(asset.Digest, out var digest))
        {
            await VerifyHashAsync(filePath, digest, ct).ConfigureAwait(false);
            return;
        }

        // No digest on the asset: fall back to sha256sum.txt, which is a separate file
        // fetched separately and therefore weaker evidence than the digest.
        var checksumAsset = FrpcReleaseClient.FindChecksumAsset(release);
        if (checksumAsset is null) return;

        var url = string.IsNullOrWhiteSpace(mirror)
            ? checksumAsset.DownloadUrl
            : FrpcReleaseClient.ToMirrorUrl(checksumAsset.DownloadUrl, mirror);

        string content;
        try
        {
            content = await http.GetStringAsync(url, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or TaskCanceledException)
        {
            return;
        }

        var expected = FindHashForAsset(content, asset.Name);
        if (string.IsNullOrWhiteSpace(expected)) return;

        await VerifyHashAsync(filePath, expected, ct).ConfigureAwait(false);
    }

    private static bool TryReadDigest(string? digest, out string expected)
    {
        expected = string.Empty;
        if (string.IsNullOrWhiteSpace(digest) ||
            !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return false;

        expected = digest["sha256:".Length..].Trim();
        return expected.Length > 0;
    }

    private static string? FindHashForAsset(string content, string assetName)
    {
        string? fallback = null;

        foreach (var line in content.Split('\n'))
        {
            var parts = line.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var hash = parts[0].Trim();
            if (!Sha256Pattern().IsMatch(hash)) continue;

            fallback ??= hash;
            if (parts.Any(p => p.Contains(assetName, StringComparison.OrdinalIgnoreCase))) return hash;
        }

        return fallback;
    }

    private static async Task VerifyHashAsync(string filePath, string expected, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false));

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("下载的文件校验失败,可能已损坏或被篡改");
    }

    [GeneratedRegex("^[0-9A-Fa-f]{64}$")]
    private static partial Regex Sha256Pattern();
}
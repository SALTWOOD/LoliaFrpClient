using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LoliaFrpClient.Core.Frpc;

// Releases live on GitHub, not on api.lolia.link, so this deliberately bypasses ApiSession:
// different auth, different retry semantics, different error codes.
public static partial class FrpcReleaseClient
{
    public const string ReleaseApiUrl = "https://api.github.com/repos/Lolia-FRP/lolia-frp/releases/latest";

    public const string DefaultMirror = "https://hub.locyancs.cn";

    // Returns null instead of throwing: update checking is a side path that may fail silently.
    public static async Task<FrpcRelease?> TryGetLatestAsync(HttpClient http, CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            using var response = await http.GetAsync(ReleaseApiUrl, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text)) return null;

            var dto = JsonSerializer.Deserialize(text, FrpcJsonContext.Default.GitHubReleaseDto);
            return dto is null ? null : Parse(dto);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException
                                       or TaskCanceledException)
        {
            return null;
        }
    }

    public static FrpcAssetSelection SelectBestAsset(FrpcRelease release)
    {
        var (platform, architecture) = GetCurrentPlatform();
        var candidates = release.Assets.Where(a => !IsChecksumAsset(a.Name)).ToList();

        var matched = candidates
            .Where(a => a.Name.StartsWith($"LoliaFrp_{platform}_{architecture}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Fall back to substring matching in case the naming scheme changes.
        if (matched.Count == 0)
            matched = candidates
                .Where(a => a.Name.Contains($"{platform}_{architecture}", StringComparison.OrdinalIgnoreCase))
                .ToList();

        // Windows is the only platform shipping a zip; everything else is tar.gz.
        var preferred = OperatingSystem.IsWindows() ? ".zip" : ".tar.gz";
        var asset = matched.FirstOrDefault(a => a.Name.EndsWith(preferred, StringComparison.OrdinalIgnoreCase))
                    ?? matched.FirstOrDefault()
                    ?? throw new InvalidOperationException($"该版本没有适用于 {platform}/{architecture} 的 frpc 资产");

        return new FrpcAssetSelection
        {
            Version = release.Version,
            Platform = platform,
            Architecture = architecture,
            Asset = asset
        };
    }

    public static (string Platform, string Architecture) GetCurrentPlatform()
    {
        var architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => "386",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            _ => "amd64"
        };

        // Android is Linux underneath, so it has to be tested before the linux fallback:
        // asking IsOSPlatform(Linux) on Android answers yes, and the linux build cannot run there.
        // OperatingSystem.* rather than RuntimeInformation because this is called from code the
        // trimmer processes.
        var platform = OperatingSystem.IsWindows() ? "windows"
            : OperatingSystem.IsMacOS() ? "darwin"
            : OperatingSystem.IsAndroid() ? "android"
            : "linux";

        return (platform, architecture);
    }

    public static bool IsChecksumAsset(string name)
    {
        return name.Equals("sha256sum.txt", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".sha256.txt", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".md5", StringComparison.OrdinalIgnoreCase);
    }

    public static FrpcAsset? FindChecksumAsset(FrpcRelease release)
    {
        return release.Assets.FirstOrDefault(a => IsChecksumAsset(a.Name));
    }

    public static string ToMirrorUrl(string downloadUrl, string? mirror)
    {
        const string githubPrefix = "https://github.com/";
        if (!downloadUrl.StartsWith(githubPrefix, StringComparison.OrdinalIgnoreCase)) return downloadUrl;

        var host = string.IsNullOrWhiteSpace(mirror) ? DefaultMirror : mirror.TrimEnd('/');
        return $"{host}/github.com/{downloadUrl[githubPrefix.Length..]}";
    }

    public static string ExtractVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return string.Empty;

        var match = VersionPattern().Match(tag);
        return match.Success ? match.Groups[1].Value : tag.TrimStart('v');
    }

    private static FrpcRelease Parse(GitHubReleaseDto dto)
    {
        return new FrpcRelease
        {
            Version = ExtractVersion(dto.TagName),
            TagName = dto.TagName ?? string.Empty,
            Assets = (dto.Assets ?? [])
                .Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))
                .Select(a => new FrpcAsset
                {
                    Name = a.Name!,
                    DownloadUrl = a.BrowserDownloadUrl!,
                    Digest = a.Digest
                })
                .ToList()
        };
    }

    [GeneratedRegex(@"v?(\d+\.\d+\.\d+[0-9A-Za-z.\-]*)")]
    private static partial Regex VersionPattern();
}

internal sealed class GitHubReleaseDto
{
    [JsonPropertyName("tag_name")] public string? TagName { get; set; }

    [JsonPropertyName("assets")] public List<GitHubAssetDto>? Assets { get; set; }
}

internal sealed class GitHubAssetDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("digest")] public string? Digest { get; set; }
}

[JsonSerializable(typeof(GitHubReleaseDto))]
internal sealed partial class FrpcJsonContext : JsonSerializerContext
{
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LoliaFrpClient.Services;

public record GitHubAsset(string Name, string BrowserDownloadUrl, long Size);

public record GitHubRelease
{
    [JsonPropertyName("tag_name")] public required string TagName { get; set; }
    public required string Name  { get; set; }
    public required string Body  { get; set; }
    public required List<GitHubAsset> Assets  { get; set; }
}

public class GitHubReleaseService
{
    private const string ApiBase = "https://api.github.com/repos";
    private static readonly HttpClient _http = new();

    static GitHubReleaseService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LoliaFrpClient/1.0");
    }

    public static async Task<GitHubRelease?> GetLatestReleaseAsync(string owner, string repo)
    {
        try
        {
            // API calls always go to GitHub directly
            return await _http.GetFromJsonAsync<GitHubRelease>($"{ApiBase}/{owner}/{repo}/releases/latest");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fetch release failed: {ex.Message}");
            return null;
        }
    }

    public static string? GetDownloadUrl(GitHubRelease release, AssetType type)
    {
        var platformFile = GetPlatformFileName(release.TagName, type);
        var asset = release.Assets.FirstOrDefault(a => a.Name.Equals(platformFile, StringComparison.OrdinalIgnoreCase));
        
        if (asset == null) return null;

        (string owner, string repo) = type switch
        {
            AssetType.Client => ("SALTWOOD", "LoliaFrpClient"),
            AssetType.Frpc => ("Lolia-FRP", "lolia-frp"),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        var template = SettingsStorage.Instance.DownloadUrlTemplate;

        return template
            .Replace("{owner}", owner)
            .Replace("{repo}", repo)
            .Replace("{tag}", release.TagName)
            .Replace("{asset}", asset.Name);
    }

    private static string GetPlatformFileName(string tag, AssetType type)
    {
        string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin" :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : "unknown";

        string arch = (type, RuntimeInformation.ProcessArchitecture) switch
        {
            (AssetType.Frpc, Architecture.X64) => "amd64",
            (AssetType.Frpc, Architecture.X86) => "386",
            (_, Architecture.X64) => "x64",
            (_, Architecture.X86) => "x86",
            (_, Architecture.Arm64) => "arm64",
            _ => "unknown"
        };

        string ext = os == "win" ? "zip" : "tar.gz";

        return type switch
        {
            AssetType.Client => $"LoliaFrpClient_{tag}_{os}-{arch}.{ext}",
            AssetType.Frpc => $"LoliaFrp_{(os == "win" ? "windows" : os)}_{arch}.{ext}",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}

public enum AssetType
{
    Client,
    Frpc
}
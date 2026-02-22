using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LoliaFrpClient.Services;

/// <summary>
///     GitHub Release 资产信息
/// </summary>
public class GitHubAsset
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")] public long Size { get; set; }
}

/// <summary>
///     GitHub Release 信息
/// </summary>
public class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;

    [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("published_at")] public DateTime PublishedAt { get; set; }

    [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = new();
}

/// <summary>
///     客户端版本响应数据
/// </summary>
public class ClientVersionData
{
    [JsonPropertyName("tag")] public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
}

/// <summary>
///     客户端版本 API 响应
/// </summary>
public class ClientVersionResponse
{
    [JsonPropertyName("code")] public int Code { get; set; }

    [JsonPropertyName("msg")] public string Msg { get; set; } = string.Empty;

    [JsonPropertyName("data")] public ClientVersionData? Data { get; set; }
}

/// <summary>
///     GitHub Release 服务，用于从 GitHub 获取发布版本信息
/// </summary>
public class GitHubReleaseService
{
    private const string GitHubApiBaseUrl = "https://api.github.com/repos";
    private const string UserAgent = "LoliaFrpClient/1.0";

    // 镜像源配置
    private const string MirrorDirect = "https://github.com";
    private const string MirrorCdnAkaere = "https://cdn.akaere.online/github.com";
    private static readonly HttpClient _httpClient = new();

    static GitHubReleaseService()
    {
        // GitHub API 强制要求 User-Agent
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

    /// <summary>
    ///     获取当前配置的镜像源前缀
    /// </summary>
    public static string GetMirrorPrefix()
    {
        var mirrorType = SettingsStorage.Instance.GitHubMirrorType;
        return mirrorType switch
        {
            1 => MirrorCdnAkaere,
            _ => MirrorDirect
        };
    }

    /// <summary>
    ///     转换 GitHub 下载 URL 为镜像 URL
    ///     注意：仅转换 github.com 的下载链接，不转换 api.github.com 的 API 请求
    /// </summary>
    /// <param name="originalUrl">原始 GitHub URL</param>
    /// <returns>转换后的镜像 URL</returns>
    public static string ConvertToMirrorUrl(string originalUrl)
    {
        if (string.IsNullOrEmpty(originalUrl))
            return originalUrl;

        var mirrorType = SettingsStorage.Instance.GitHubMirrorType;

        if (mirrorType == 1)
            // 使用 cdn.akaere.online 镜像（仅用于 github.com 下载链接）
            if (originalUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
                return MirrorCdnAkaere + originalUrl.Substring("https://github.com".Length);

        // 注意：api.github.com 的 API 请求不使用镜像源
        return originalUrl;
    }

    /// <summary>
    ///     从 API 获取最新版本信息
    /// </summary>
    /// <returns>版本标签，如 "v0.67.0"</returns>
    public static async Task<string?> GetLatestVersionFromApiAsync()
    {
        try
        {
            var apiClient = ApiClientProvider.Instance.Client;
            var versionRequestBuilder = apiClient.Client.Version;

            var response = await versionRequestBuilder.GetAsync();

            if (response != null)
            {
                var code = response.Code;
                if (code == 200 && response.Data != null) return response.Data.Tag;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    ///     获取仓库的最新 Release
    ///     注意：API 请求始终直接访问 GitHub，不使用镜像源
    /// </summary>
    /// <param name="owner">仓库所有者</param>
    /// <param name="repo">仓库名称</param>
    /// <returns>最新 Release 信息</returns>
    public static async Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repo)
    {
        // API 请求始终直接访问 GitHub，不使用镜像源
        var url = $"{GitHubApiBaseUrl}/{owner}/{repo}/releases/latest";

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"获取最新 Release 失败: {response.StatusCode} - {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var release = JsonSerializer.Deserialize(
            responseContent,
            AppJsonContext.Default.GitHubRelease
        );

        return release ?? throw new Exception("解析 Release 响应失败");
    }

    /// <summary>
    ///     获取仓库的所有 Releases
    ///     注意：API 请求始终直接访问 GitHub，不使用镜像源
    /// </summary>
    /// <param name="owner">仓库所有者</param>
    /// <param name="repo">仓库名称</param>
    /// <param name="limit">限制返回数量</param>
    /// <returns>Release 列表</returns>
    public static async Task<List<GitHubRelease>> GetReleasesAsync(string owner, string repo, int limit = 10)
    {
        // API 请求始终直接访问 GitHub，不使用镜像源
        var url = $"{GitHubApiBaseUrl}/{owner}/{repo}/releases?per_page={limit}";

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"获取 Releases 列表失败: {response.StatusCode} - {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var releases = JsonSerializer.Deserialize(
            responseContent,
            AppJsonContext.Default.ListGitHubRelease
        );

        return releases ?? new List<GitHubRelease>();
    }

    /// <summary>
    ///     根据平台获取对应的下载 URL（应用镜像源）
    /// </summary>
    /// <param name="release">Release 信息</param>
    /// <returns>下载 URL，如果找不到则返回 null</returns>
    public static string? GetDownloadUrlForPlatform(GitHubRelease release)
    {
        var platform = GetPlatformIdentifier();

        // 查找匹配当前平台的资产
        var asset = release.Assets.FirstOrDefault(a => a.Name.Equals(platform, StringComparison.OrdinalIgnoreCase));

        if (asset == null)
            return null;

        // 应用镜像源转换
        return ConvertToMirrorUrl(asset.BrowserDownloadUrl);
    }

    /// <summary>
    ///     获取当前平台标识符
    /// </summary>
    /// <returns>平台标识符</returns>
    private static string GetPlatformIdentifier()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        var isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        var isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        var isX64 = RuntimeInformation.ProcessArchitecture == Architecture.X64;
        var isX86 = RuntimeInformation.ProcessArchitecture == Architecture.X86;

        if (isWindows)
        {
            if (isArm64) return "LoliaFrp_windows_arm64.zip";
            if (isX64) return "LoliaFrp_windows_amd64.zip";
            if (isX86) return "LoliaFrp_windows_386.zip";
        }
        else if (isLinux)
        {
            if (isArm64) return "LoliaFrp_linux_arm64.tar.gz";
            if (isX64) return "LoliaFrp_linux_amd64.tar.gz";
            if (isX86) return "LoliaFrp_linux_386.tar.gz";
        }
        else if (isMacOS)
        {
            if (isArm64) return "LoliaFrp_darwin_arm64.tar.gz";
            if (isX64) return "LoliaFrp_darwin_amd64.tar.gz";
        }

        return "unknown";
    }
}
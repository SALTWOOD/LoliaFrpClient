using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LoliaFrpClient.Services;

/// <summary>
///     客户端更新检查结果
/// </summary>
public class ClientUpdateResult
{
    /// <summary>
    ///     是否有可用更新
    /// </summary>
    public bool HasUpdate { get; set; }

    /// <summary>
    ///     当前版本
    /// </summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    ///     最新版本
    /// </summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>
    ///     发布页面 URL
    /// </summary>
    public string ReleaseUrl { get; set; } = string.Empty;

    /// <summary>
    ///     更新日志
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    ///     发布时间
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    ///     下载 URL（如果有匹配的平台包）
    /// </summary>
    public string? DownloadUrl { get; set; }
}

/// <summary>
///     客户端本体更新检查服务
/// </summary>
public class ClientUpdateService
{
    private const string Owner = "SALTWOOD";
    private const string Repo = "LoliaFrpClient";

    /// <summary>
    ///     获取当前客户端版本
    /// </summary>
    /// <returns>版本字符串，如 "1.0.0"</returns>
    public static string GetCurrentVersion()
    {
        // 尝试从程序集版本获取
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyVersion = assembly.GetName().Version;

        if (assemblyVersion != null) return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";

        // 回退到手动版本
        return "1.0.0";
    }

    /// <summary>
    ///     比较两个版本号
    /// </summary>
    /// <param name="version1">版本1，如 "1.0.0" 或 "v1.0.0"</param>
    /// <param name="version2">版本2，如 "1.0.0" 或 "v1.0.0"</param>
    /// <returns>如果 version1 小于 version2 返回负数，相等返回 0，大于返回正数</returns>
    public static int CompareVersions(string version1, string version2)
    {
        // 移除 'v' 前缀
        version1 = version1.TrimStart('v', 'V');
        version2 = version2.TrimStart('v', 'V');

        var parts1 = version1.Split('.');
        var parts2 = version2.Split('.');

        var maxLength = Math.Max(parts1.Length, parts2.Length);

        for (var i = 0; i < maxLength; i++)
        {
            var v1 = i < parts1.Length && int.TryParse(parts1[i], out var p1) ? p1 : 0;
            var v2 = i < parts2.Length && int.TryParse(parts2[i], out var p2) ? p2 : 0;

            if (v1 != v2) return v1.CompareTo(v2);
        }

        return 0;
    }

    /// <summary>
    ///     检查客户端更新
    /// </summary>
    /// <returns>更新检查结果</returns>
    public static async Task<ClientUpdateResult> CheckForUpdateAsync()
    {
        var result = new ClientUpdateResult
        {
            CurrentVersion = GetCurrentVersion()
        };

        try
        {
            // 从 GitHub 获取最新 Release
            var release = await GitHubReleaseService.GetLatestReleaseAsync(Owner, Repo);

            if (release != null)
            {
                result.LatestVersion = release.TagName;
                result.ReleaseUrl = release.HtmlUrl;
                result.ReleaseNotes = release.Body;
                result.PublishedAt = release.PublishedAt;

                // 比较版本
                var comparison = CompareVersions(result.CurrentVersion, result.LatestVersion);
                result.HasUpdate = comparison < 0;

                // 获取下载 URL
                result.DownloadUrl = GetDownloadUrlForPlatform(release);
            }
        }
        catch (Exception)
        {
            // 检查失败时返回无更新
            result.HasUpdate = false;
        }

        return result;
    }

    /// <summary>
    ///     根据平台获取对应的下载 URL
    /// </summary>
    /// <param name="release">Release 信息</param>
    /// <returns>下载 URL，如果找不到则返回 null</returns>
    private static string? GetDownloadUrlForPlatform(GitHubRelease release)
    {
        var platformPattern = GetPlatformPattern();

        foreach (var asset in release.Assets)
            if (asset.Name.Contains(platformPattern, StringComparison.OrdinalIgnoreCase))
                return GitHubReleaseService.ConvertToMirrorUrl(asset.BrowserDownloadUrl);

        return null;
    }

    /// <summary>
    ///     获取当前平台的匹配模式
    /// </summary>
    /// <returns>平台模式字符串</returns>
    private static string GetPlatformPattern()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(
            OSPlatform.Windows);
        var isLinux = RuntimeInformation.IsOSPlatform(
            OSPlatform.Linux);
        var isMacOS = RuntimeInformation.IsOSPlatform(
            OSPlatform.OSX);

        var isArm64 = RuntimeInformation.ProcessArchitecture ==
                      Architecture.Arm64;
        var isX64 = RuntimeInformation.ProcessArchitecture ==
                    Architecture.X64;
        var isX86 = RuntimeInformation.ProcessArchitecture ==
                    Architecture.X86;

        if (isWindows)
        {
            if (isX64) return "win-x64";
            if (isArm64) return "win-arm64";
            if (isX86) return "win-x86";
            return "win";
        }

        if (isLinux)
        {
            if (isX64) return "linux-x64";
            if (isArm64) return "linux-arm64";
            if (isX86) return "linux-x86";
            return "linux";
        }

        if (isMacOS)
        {
            if (isArm64) return "osx-arm64";
            if (isX64) return "osx-x64";
            return "osx";
        }

        return "unknown";
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LoliaFrpClient.Services
{
    /// <summary>
    /// GitHub Release 资产信息
    /// </summary>
    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    /// <summary>
    /// GitHub Release 信息
    /// </summary>
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = new List<GitHubAsset>();
    }

    /// <summary>
    /// GitHub Release 服务，用于从 GitHub 获取发布版本信息
    /// </summary>
    public class GitHubReleaseService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string GitHubApiBaseUrl = "https://api.github.com/repos";
        private const string UserAgent = "LoliaFrpClient/1.0";

        static GitHubReleaseService()
        {
            // GitHub API 强制要求 User-Agent
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        }

        /// <summary>
        /// 获取仓库的最新 Release
        /// </summary>
        /// <param name="owner">仓库所有者</param>
        /// <param name="repo">仓库名称</param>
        /// <returns>最新 Release 信息</returns>
        public static async Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repo)
        {
            var url = $"{GitHubApiBaseUrl}/{owner}/{repo}/releases/latest";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"获取最新 Release 失败: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubRelease>(responseContent);

            return release ?? throw new Exception("解析 Release 响应失败");
        }

        /// <summary>
        /// 获取仓库的所有 Releases
        /// </summary>
        /// <param name="owner">仓库所有者</param>
        /// <param name="repo">仓库名称</param>
        /// <param name="limit">限制返回数量</param>
        /// <returns>Release 列表</returns>
        public static async Task<List<GitHubRelease>> GetReleasesAsync(string owner, string repo, int limit = 10)
        {
            var url = $"{GitHubApiBaseUrl}/{owner}/{repo}/releases?per_page={limit}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"获取 Releases 列表失败: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(responseContent);

            return releases ?? new List<GitHubRelease>();
        }

        /// <summary>
        /// 根据平台获取对应的下载 URL
        /// </summary>
        /// <param name="release">Release 信息</param>
        /// <returns>下载 URL，如果找不到则返回 null</returns>
        public static string? GetDownloadUrlForPlatform(GitHubRelease release)
        {
            var platform = GetPlatformIdentifier();
            
            // 查找匹配当前平台的资产
            var asset = release.Assets.FirstOrDefault(a => a.Name.Equals(platform, StringComparison.OrdinalIgnoreCase));
            
            return asset?.BrowserDownloadUrl;
        }

        /// <summary>
        /// 获取当前平台标识符
        /// </summary>
        /// <returns>平台标识符</returns>
        private static string GetPlatformIdentifier()
        {
            var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
            var isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var isMacOS = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);

            var isArm64 = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64;
            var isX64 = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64;
            var isX86 = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X86;

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
}

using System;
using System.Threading.Tasks;
using LoliaFrpClient.Constants;

namespace LoliaFrpClient.Services;

/// <summary>
///     frpc 核心的版本查询与安装编排。
///     <para>
///         把「查最新版本 → 选对应平台资源 → 下载安装」这条流程从设置页面中抽出，
///         页面只负责把状态渲染到控件上。
///     </para>
/// </summary>
public sealed class FrpcInstallationService
{
    private readonly FrpcManager _frpcManager;

    public FrpcInstallationService(FrpcManager frpcManager) => _frpcManager = frpcManager;

    /// <summary>当前已安装的 frpc 版本，未安装时为 null。</summary>
    public string? InstalledVersion => _frpcManager.InstalledVersion;

    /// <summary>当前是否有 frpc 进程在运行。</summary>
    public bool IsAnyProcessRunning => _frpcManager.IsAnyProcessRunning;

    /// <summary>最近一次查询到的最新发布；未查询到或查询失败时为 null。</summary>
    public GitHubRelease? LatestRelease { get; private set; }

    /// <summary>基于已知的最新版本计算安装状态。</summary>
    public FrpcInstallStatus InstallStatus => _frpcManager.GetInstallStatus(LatestRelease?.TagName);

    /// <summary>
    ///     查询 frpc 的最新发布版本。失败时返回 null，并保留上一次查到的结果。
    /// </summary>
    public async Task<GitHubRelease?> RefreshLatestReleaseAsync()
    {
        var release = await GitHubReleaseService.GetLatestReleaseAsync(
            AppConstants.FrpcReleaseOwner, AppConstants.FrpcReleaseRepo);

        if (release != null) LatestRelease = release;

        return release;
    }

    /// <summary>
    ///     下载并安装已知的最新版本。
    /// </summary>
    /// <returns>成功返回 null；失败返回可直接展示给用户的中文原因。</returns>
    public async Task<string?> TryInstallLatestAsync(IProgress<double>? progress = null)
    {
        if (LatestRelease == null) return "尚未获取到版本信息";

        var url = GitHubReleaseService.GetDownloadUrl(LatestRelease, AssetType.Frpc);
        if (url == null) return "无适用当前平台的包";

        var ok = await _frpcManager.InstallAsync(url, LatestRelease.TagName, progress);
        return ok ? null : "操作失败";
    }

    /// <summary>
    ///     卸载 frpc 并停止所有相关进程。
    /// </summary>
    public void Uninstall() => _frpcManager.UninstallFrpc();
}

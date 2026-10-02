using LoliaFrpClient.Api.Models;
using LoliaFrpClient.Core.Frpc;

namespace LoliaFrpClient.Core;

public static class ClientRelease
{
    // 服务端 release 模块里这个客户端那条记录的 ID。
    private const long ReleaseId = 2;

    public static Task<ApiResult<ReleaseVersion>> GetLatestAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<ReleaseVersionResponse, ReleaseVersion>(
            c => api.Client.Releases.Versions.Latest.GetAsync(
                r => r.QueryParameters.ReleaseId = ReleaseId, c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>
    ///     取当前平台对应的下载地址。镜像源优先,direct 兜底。
    /// </summary>
    /// <remarks>
    ///     /releases/download 在某个源没有该文件的副本时返回 409,并且文档明说不自动回退 ——
    ///     换源是调用方的责任,所以这里逐个试到成功为止。
    /// </remarks>
    public static async Task<ApiResult<ReleaseDownload>> GetDownloadAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        var sources = await ApiCall.RunAsync<ReleaseSourcesResponse, ReleaseSourcesResponse_data>(
            c => api.Client.Releases.Sources.GetAsync(
                r => r.QueryParameters.ReleaseId = ReleaseId, c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        if (sources.Data?.Items is not { Count: > 0 } choices)
            return sources.With<ReleaseDownload>(null);

        var (os, arch) = FrpcReleaseClient.GetCurrentPlatform();

        var candidates = choices
            .Where(c => !string.IsNullOrWhiteSpace(c.Source))
            .OrderBy(c => c.Type is ReleaseSourceChoice_type.Direct ? 1 : 0)
            .Select(c => c.Source!);

        ApiResult<ReleaseDownload>? last = null;

        foreach (var source in candidates)
        {
            var result = await ApiCall.RunAsync<ReleaseDownloadResponse, ReleaseDownload>(
                c => api.Client.Releases.Download.GetAsync(r =>
                {
                    r.QueryParameters.ReleaseId = ReleaseId;
                    r.QueryParameters.Source = source;
                    r.QueryParameters.Os = os;
                    r.QueryParameters.Arch = arch;
                }, c),
                r => (r.Code, r.Msg, r.Data),
                cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess) return result;

            last = result;
        }

        return last ?? new ApiResult<ReleaseDownload> { Msg = "没有可用的下载源" };
    }
}

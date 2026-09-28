using LoliaFrpClient.Api.Client.VersionNamespace;

namespace LoliaFrpClient.Core;

/// <summary>服务端发布的最新客户端版本。无需登录。</summary>
public static class ClientVersion
{
    /// <summary>取最新客户端版本及其下载资源。</summary>
    public static Task<ApiResult<VersionGetResponse_data>> GetAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<VersionGetResponse, VersionGetResponse_data>(
            c => api.Client.Client.Version.GetAsVersionGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }
}

using LoliaFrpClient.Api.Client.VersionNamespace;

namespace LoliaFrpClient.Core;

public static class ClientVersion
{
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
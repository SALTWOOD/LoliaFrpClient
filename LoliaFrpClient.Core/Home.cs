using LoliaFrpClient.Api.Home;

namespace LoliaFrpClient.Core;

public static class Home
{
    public static Task<ApiResult<HomeGetResponse_data>> GetAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<HomeGetResponse, HomeGetResponse_data>(
            c => api.Client.Home.GetAsHomeGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }
}
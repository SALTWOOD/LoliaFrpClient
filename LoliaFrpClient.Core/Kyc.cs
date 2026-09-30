using LoliaFrpClient.Api.User.Kyc.Init;
using LoliaFrpClient.Api.User.Kyc.Query;
using LoliaFrpClient.Api.User.Kyc.Status;

namespace LoliaFrpClient.Core;

public static class Kyc
{
    public static Task<ApiResult<StatusGetResponse_data>> GetStatusAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<StatusGetResponse, StatusGetResponse_data>(
            c => api.Client.User.Kyc.Status.GetAsStatusGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<QueryGetResponse_data>> QueryAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<QueryGetResponse, QueryGetResponse_data>(
            c => api.Client.User.Kyc.Query.GetAsQueryGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<QueryPostResponse_data>> QueryByOrderAsync(
        string orderNo,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<QueryPostResponse, QueryPostResponse_data>(
            c => api.Client.User.Kyc.Query.PostAsQueryPostResponseAsync(
                new QueryPostRequestBody { OrderNo = orderNo },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<InitPostResponse_data>> InitAsync(
        InitPostRequestBody request,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<InitPostResponse, InitPostResponse_data>(
            c => api.Client.User.Kyc.Init.PostAsInitPostResponseAsync(request, cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }
}
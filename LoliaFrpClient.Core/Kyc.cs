using LoliaFrpClient.Api.User.Kyc.Init;
using LoliaFrpClient.Api.User.Kyc.Query;
using LoliaFrpClient.Api.User.Kyc.Status;

namespace LoliaFrpClient.Core;

/// <summary>
///     实名认证。操作对象是当前登录用户自己的认证状态,没有独立身份,故只提供静态方法。
/// </summary>
public static class Kyc
{
    /// <summary>取实名认证状态。</summary>
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

    /// <summary>查询当前用户的认证结果。</summary>
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

    /// <summary>按订单号查询认证结果。</summary>
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

    /// <summary>提交实名认证。</summary>
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

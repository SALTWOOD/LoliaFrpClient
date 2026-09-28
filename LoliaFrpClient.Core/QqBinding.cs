using LoliaFrpClient.Api.User.Qq.Challenge;

namespace LoliaFrpClient.Core;

/// <summary>
///     当前用户与 QQ 的绑定关系。每位用户至多一条绑定,没有独立身份,故只提供静态方法。
/// </summary>
public static class QqBinding
{
    /// <summary>取已绑定的 QQ。</summary>
    public static Task<ApiResult<Api.User.Qq.QqGetResponse_data>> GetAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<Api.User.Qq.QqGetResponse, Api.User.Qq.QqGetResponse_data>(
            c => api.Client.User.Qq.GetAsQqGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>解绑 QQ。</summary>
    public static Task<ApiResult> UnbindAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<Api.User.Qq.QqDeleteResponse>(
            c => api.Client.User.Qq.DeleteAsQqDeleteResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }

    /// <summary>
    ///     创建绑定挑战值。拿到后交给机器人端调用 <see cref="Qqbot.BindAsync" /> 完成绑定。
    /// </summary>
    public static Task<ApiResult<ChallengePostResponse_data>> CreateChallengeAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<ChallengePostResponse, ChallengePostResponse_data>(
            c => api.Client.User.Qq.Challenge.PostAsChallengePostResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }
}

using LoliaFrpClient.Api.Qqbot.Qq.Bind;
using LoliaFrpClient.Api.Qqbot.Qq.Userinfo;
using QqbotCheckin = LoliaFrpClient.Api.Qqbot.Qq.Checkin;

namespace LoliaFrpClient.Core;

/// <summary>
///     QQ 机器人侧接口。以 QQ 号为入参,与当前登录用户无关,故只提供静态方法。
/// </summary>
public static class Qqbot
{
    /// <summary>绑定 QQ。</summary>
    public static Task<ApiResult<BindPostResponse_data>> BindAsync(
        string qq,
        string challenge,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<BindPostResponse, BindPostResponse_data>(
            c => api.Client.Qqbot.Qq.Bind.PostAsBindPostResponseAsync(
                new BindPostRequestBody { Qq = qq, Challenge = challenge },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>通过 QQ 号查询用户信息。</summary>
    public static Task<ApiResult<UserinfoPostResponse_data>> GetUserInfoAsync(
        string qq,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<UserinfoPostResponse, UserinfoPostResponse_data>(
            c => api.Client.Qqbot.Qq.Userinfo.PostAsUserinfoPostResponseAsync(
                new UserinfoPostRequestBody { Qq = qq },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>QQ 签到。</summary>
    public static Task<ApiResult<QqbotCheckin.CheckinPostResponse_data>> CheckInAsync(
        string qq,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<QqbotCheckin.CheckinPostResponse, QqbotCheckin.CheckinPostResponse_data>(
            c => api.Client.Qqbot.Qq.Checkin.PostAsCheckinPostResponseAsync(
                new QqbotCheckin.CheckinPostRequestBody { Qq = qq },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }
}

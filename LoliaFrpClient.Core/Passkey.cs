using LoliaFrpClient.Api.User.Passkey;
using PasskeyBindBegin = LoliaFrpClient.Api.User.Passkey.Bind.Begin;
using PasskeyBindFinish = LoliaFrpClient.Api.User.Passkey.Bind.Finish;
using PasskeyItem = LoliaFrpClient.Api.User.Passkey.Item;
using PasskeyLoginBegin = LoliaFrpClient.Api.User.Passkey.Login.Begin;
using PasskeyLoginFinish = LoliaFrpClient.Api.User.Passkey.Login.Finish;

namespace LoliaFrpClient.Core;

/// <summary>
///     Passkey(WebAuthn 凭据)。身份是凭据 ID。
///     <para>
///         绑定与登录各自是「begin / finish」两步,需要平台认证器配合完成挑战,
///         包装层只负责把两步的请求送到服务端。
///     </para>
/// </summary>
public sealed class Passkey : ApiFacade
{
    /// <summary>用当前会话创建指向指定凭据的引用。</summary>
    public Passkey(int id) : this(id, ApiSession.Current)
    {
    }

    /// <summary>用指定会话创建指向指定凭据的引用。</summary>
    public Passkey(int id, ApiSession session) : base(session)
    {
        Id = id;
    }

    /// <summary>凭据 ID。</summary>
    public int Id { get; }

    /// <summary>取本用户已绑定的全部 Passkey。</summary>
    public static Task<ApiResult<PasskeyGetResponse_data>> ListAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<PasskeyGetResponse, PasskeyGetResponse_data>(
            c => api.Client.User.Passkey.GetAsPasskeyGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>绑定 Passkey —— 第一步,取挑战。</summary>
    public static Task<ApiResult<PasskeyBindBegin.BeginPostResponse_data>> BeginBindAsync(
        string name,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<PasskeyBindBegin.BeginPostResponse, PasskeyBindBegin.BeginPostResponse_data>(
            c => api.Client.User.Passkey.Bind.Begin.PostAsBeginPostResponseAsync(
                new PasskeyBindBegin.BeginPostRequestBody { Name = name },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>绑定 Passkey —— 第二步,提交认证器签名结果。</summary>
    public static Task<ApiResult<PasskeyBindFinish.FinishPostResponse_data>> FinishBindAsync(
        PasskeyBindFinish.FinishPostRequestBody result,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<PasskeyBindFinish.FinishPostResponse, PasskeyBindFinish.FinishPostResponse_data>(
            c => api.Client.User.Passkey.Bind.Finish.PostAsFinishPostResponseAsync(result, cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>Passkey 登录 —— 第一步,取挑战。</summary>
    public static Task<ApiResult<PasskeyLoginBegin.BeginPostResponse_data>> BeginLoginAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<PasskeyLoginBegin.BeginPostResponse, PasskeyLoginBegin.BeginPostResponse_data>(
            c => api.Client.User.Passkey.Login.Begin.PostAsBeginPostResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>Passkey 登录 —— 第二步,提交认证器签名结果。</summary>
    public static Task<ApiResult<PasskeyLoginFinish.FinishPostResponse_data>> FinishLoginAsync(
        PasskeyLoginFinish.FinishPostRequestBody result,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<PasskeyLoginFinish.FinishPostResponse, PasskeyLoginFinish.FinishPostResponse_data>(
            c => api.Client.User.Passkey.Login.Finish.PostAsFinishPostResponseAsync(result, cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>删除本凭据。</summary>
    public Task<ApiResult> DeleteAsync(CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<PasskeyItem.WithPasskey_DeleteResponse>(
            c => Client.User.Passkey[Id].DeleteAsWithPasskey_DeleteResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
}

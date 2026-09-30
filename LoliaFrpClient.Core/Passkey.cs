using LoliaFrpClient.Api.User.Passkey;
using PasskeyBindBegin = LoliaFrpClient.Api.User.Passkey.Bind.Begin;
using PasskeyBindFinish = LoliaFrpClient.Api.User.Passkey.Bind.Finish;
using PasskeyItem = LoliaFrpClient.Api.User.Passkey.Item;
using PasskeyLoginBegin = LoliaFrpClient.Api.User.Passkey.Login.Begin;
using PasskeyLoginFinish = LoliaFrpClient.Api.User.Passkey.Login.Finish;

namespace LoliaFrpClient.Core;

public sealed class Passkey : ApiFacade
{
    public Passkey(int id) : this(id, ApiSession.Current)
    {
    }

    public Passkey(int id, ApiSession session) : base(session)
    {
        Id = id;
    }

    public int Id { get; }

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

    public Task<ApiResult> DeleteAsync(CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<PasskeyItem.WithPasskey_DeleteResponse>(
            c => Client.User.Passkey[Id].DeleteAsWithPasskey_DeleteResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }
}
using LoliaFrpClient.Api.User.Oauth.App;
using LoliaFrpClient.Api.User.Oauth.Apps;
using OAuthAppItem = LoliaFrpClient.Api.User.Oauth.App.Item;
using OAuthAppResetSecret = LoliaFrpClient.Api.User.Oauth.App.Item.ResetSecret;

namespace LoliaFrpClient.Core;

public sealed class OAuthApp : ApiFacade
{
    public OAuthApp(int id) : this(id, ApiSession.Current)
    {
    }

    public OAuthApp(int id, ApiSession session) : base(session)
    {
        Id = id;
    }

    public int Id { get; }

    public string? Name { get; private init; }

    public string? ClientId { get; private init; }

    public static async Task<ApiResult<IReadOnlyList<OAuthApp>>> ListAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        var result = await ApiCall.RunAsync<AppsGetResponse, AppsGetResponse_data>(
            c => api.Client.User.Oauth.Apps.GetAsAppsGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<OAuthApp>? apps = result.IsSuccess
            ?
            [
                .. (result.Data?.Apps ?? []).Select(item => new OAuthApp(item.Id ?? 0, api)
                {
                    Name = item.Name,
                    ClientId = item.ClientId
                })
            ]
            : null;

        return result.With(apps);
    }

    public static async Task<ApiResult<OAuthApp>> CreateAsync(
        AppPostRequestBody specification,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        var result = await ApiCall.RunAsync<AppPostResponse, AppPostResponse_data>(
            c => api.Client.User.Oauth.App.PostAsAppPostResponseAsync(specification, cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        return result.With(result.IsSuccess && result.Data is { } data ? new OAuthApp(0, api) : null);
    }

    public Task<ApiResult> UpdateAsync(
        OAuthAppItem.AppPutRequestBody changes,
        CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<OAuthAppItem.AppPutResponse>(
            c => Client.User.Oauth.App[Id].PutAsAppPutResponseAsync(changes, cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }

    public Task<ApiResult> DeleteAsync(CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<OAuthAppItem.AppDeleteResponse>(
            c => Client.User.Oauth.App[Id].DeleteAsAppDeleteResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }

    public Task<ApiResult<OAuthAppResetSecret.ResetSecretPostResponse_data>> ResetSecretAsync(
        CancellationToken cancellationToken = default)
    {
        return ApiCall
            .RunAsync<OAuthAppResetSecret.ResetSecretPostResponse, OAuthAppResetSecret.ResetSecretPostResponse_data>(
                c => Client.User.Oauth.App[Id].ResetSecret.PostAsResetSecretPostResponseAsync(cancellationToken: c),
                r => (r.Code, r.Msg, r.Data),
                cancellationToken);
    }
}
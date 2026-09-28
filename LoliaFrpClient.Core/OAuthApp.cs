using LoliaFrpClient.Api.User.Oauth.App;
using LoliaFrpClient.Api.User.Oauth.Apps;
using OAuthAppItem = LoliaFrpClient.Api.User.Oauth.App.Item;
using OAuthAppResetSecret = LoliaFrpClient.Api.User.Oauth.App.Item.ResetSecret;

namespace LoliaFrpClient.Core;

/// <summary>
///     用户自己创建的 OAuth 应用。身份是应用 ID。
/// </summary>
public sealed class OAuthApp : ApiFacade
{
    /// <summary>用当前会话创建指向指定应用的引用。</summary>
    public OAuthApp(int id) : this(id, ApiSession.Current)
    {
    }

    /// <summary>用指定会话创建指向指定应用的引用。</summary>
    public OAuthApp(int id, ApiSession session) : base(session)
    {
        Id = id;
    }

    /// <summary>应用 ID。</summary>
    public int Id { get; }

    /// <summary>应用名。仅在 <see cref="ListAsync" /> 与 <see cref="CreateAsync" /> 的返回中填充。</summary>
    public string? Name { get; private init; }

    /// <summary>客户端 ID。仅在 <see cref="ListAsync" /> 与 <see cref="CreateAsync" /> 的返回中填充。</summary>
    public string? ClientId { get; private init; }

    /// <summary>取我创建的全部 OAuth 应用。</summary>
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
            ? [.. (result.Data?.Apps ?? []).Select(item => new OAuthApp(item.Id ?? 0, api)
            {
                Name = item.Name,
                ClientId = item.ClientId
            })]
            : null;

        return result.With(apps);
    }

    /// <summary>创建 OAuth 应用。</summary>
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

    /// <summary>更新本应用。</summary>
    public Task<ApiResult> UpdateAsync(
        OAuthAppItem.AppPutRequestBody changes,
        CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<OAuthAppItem.AppPutResponse>(
            c => Client.User.Oauth.App[Id].PutAsAppPutResponseAsync(changes, cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);

    /// <summary>删除本应用。</summary>
    public Task<ApiResult> DeleteAsync(CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<OAuthAppItem.AppDeleteResponse>(
            c => Client.User.Oauth.App[Id].DeleteAsAppDeleteResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);

    /// <summary>重置本应用的密钥。新密钥在返回体中。</summary>
    public Task<ApiResult<OAuthAppResetSecret.ResetSecretPostResponse_data>> ResetSecretAsync(
        CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<OAuthAppResetSecret.ResetSecretPostResponse, OAuthAppResetSecret.ResetSecretPostResponse_data>(
            c => Client.User.Oauth.App[Id].ResetSecret.PostAsResetSecretPostResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
}

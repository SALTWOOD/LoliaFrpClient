using LoliaFrpClient.Api.Oauth2.Approve;
using LoliaFrpClient.Api.Oauth2.Authorize;
using LoliaFrpClient.Api.Oauth2.Revoke;
using LoliaFrpClient.Api.Oauth2.Token;

namespace LoliaFrpClient.Core;

/// <summary>
///     OAuth2 授权服务端接口,即本服务作为身份提供方对外暴露的那一套。
///     <para>
///         注意与 <see cref="OAuthClient" /> 区分:后者是本客户端用 PKCE 流程去换取令牌;
///         这里则是第三方应用来向本服务申请授权时走的端点。
///     </para>
/// </summary>
public static class OAuth2
{
    /// <summary>取授权页信息,用于渲染授权确认界面。</summary>
    public static Task<ApiResult<AuthorizeGetResponse_data>> GetAuthorizationRequestAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<AuthorizeGetResponse, AuthorizeGetResponse_data>(
            c => api.Client.Oauth2.Authorize.GetAsAuthorizeGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>确认或拒绝一次授权请求。</summary>
    public static Task<ApiResult<ApprovePostResponse_data>> ApproveAsync(
        ApprovePostRequestBody request,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<ApprovePostResponse, ApprovePostResponse_data>(
            c => api.Client.Oauth2.Approve.PostAsApprovePostResponseAsync(request, cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>
    ///     撤销令牌。
    ///     <para>按 RFC 7009,该端点成功时不返回响应体,因此没有业务码可读。</para>
    /// </summary>
    public static Task<ApiResult> RevokeAsync(
        RevokePostRequestBody request,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<RevokePostResponse>(
            c => api.Client.Oauth2.Revoke.PostAsRevokePostResponseAsync(request, cancellationToken: c),
            _ => ((int?)null, (string?)null),
            cancellationToken);
    }

    /// <summary>
    ///     获取或刷新访问令牌。
    ///     <para>
    ///         该端点返回的是 RFC 6749 定义的扁平结构(<c>access_token</c> / <c>refresh_token</c> 等),
    ///         没有业务 API 的 <c>{code,msg,data}</c> 信封,因此没有业务码可读——请求返回即视为成功。
    ///     </para>
    /// </summary>
    public static Task<ApiResult<TokenPostResponse>> GetTokenAsync(
        TokenPostRequestBody request,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<TokenPostResponse, TokenPostResponse>(
            c => api.Client.Oauth2.Token.PostAsTokenPostResponseAsync(request, cancellationToken: c),
            r => ((int?)null, (string?)null, r),
            cancellationToken);
    }
}

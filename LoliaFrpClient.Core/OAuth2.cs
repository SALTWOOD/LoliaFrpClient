using LoliaFrpClient.Api.Oauth2.Approve;
using LoliaFrpClient.Api.Oauth2.Authorize;
using LoliaFrpClient.Api.Oauth2.Revoke;
using LoliaFrpClient.Api.Oauth2.Token;

namespace LoliaFrpClient.Core;

public static class OAuth2
{
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
namespace LoliaFrpClient.Core;

/// <summary>
///     OAuth2 公共客户端配置(Authorization Code + PKCE,无 client_secret)。
/// </summary>
public sealed class OAuthOptions
{
    /// <summary>授权页地址,浏览器跳转用。</summary>
    public string AuthorizeEndpoint { get; init; } = "https://dash.lolia.link/oauth/authorize";

    /// <summary>令牌端点。</summary>
    public string TokenEndpoint { get; init; } = "https://api.lolia.link/api/v1/oauth2/token";

    /// <summary>客户端 ID。</summary>
    public string ClientId { get; init; } = "goibzooz0s14ntgc";

    /// <summary>授权范围。</summary>
    public string Scope { get; init; } = "all";

    /// <summary>本地回调监听端口。</summary>
    public int CallbackPort { get; init; } = 56721;

    /// <summary>本地回调路径。</summary>
    public string CallbackPath { get; init; } = "/callback";

    /// <summary>本地回调完整地址,必须与授权请求中的 redirect_uri 完全一致。</summary>
    public string CallbackUri => $"http://localhost:{CallbackPort}{CallbackPath}";
}

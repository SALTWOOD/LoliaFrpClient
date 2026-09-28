namespace LoliaFrpClient.Core;

/// <summary>
///     当前持有凭证的来源。
///     <para>
///         服务端区分两套令牌:用户 JWT(由 <c>/user/login</c> 签发)与 OAuth2 Access Token
///         (由 <c>/oauth2/token</c> 签发)。文档明确部分接口(如签到)对 OAuth2 令牌返回
///         403「此接口不支持OAuth2访问」,因此需要记住当前用的是哪一种。
///     </para>
/// </summary>
public enum TokenOrigin
{
    /// <summary>未持有凭证。</summary>
    None = 0,

    /// <summary>用户 JWT,来自 <c>/user/login</c>。权限最完整。</summary>
    UserJwt,

    /// <summary>OAuth2 Access Token,来自 <c>/oauth2/token</c>。部分接口不可用。</summary>
    OAuth2
}

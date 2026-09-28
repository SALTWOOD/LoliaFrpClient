namespace LoliaFrpClient.Core;

/// <summary>
///     凭证存储。
///     <para>
///         读写会发生在请求线程上(认证处理器每次请求都要读 AccessToken),
///         实现需保证线程安全。
///     </para>
/// </summary>
public interface ITokenStore
{
    /// <summary>访问令牌。被认证处理器在每次请求时读取。</summary>
    string? AccessToken { get; set; }

    /// <summary>刷新令牌。仅 OAuth2 流程会用到;用户 JWT 登录时为 <c>null</c>。</summary>
    string? RefreshToken { get; set; }

    /// <summary>当前令牌的来源。</summary>
    TokenOrigin Origin { get; set; }

    /// <summary>清空全部凭证(登出时调用)。</summary>
    void Clear();
}

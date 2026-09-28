using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace LoliaFrpClient.Core;

/// <summary>
///     把 <see cref="ITokenStore" /> 里的访问令牌注入到每个请求的
///     <c>Authorization: Bearer &lt;token&gt;</c> 头。
/// </summary>
/// <remarks>
///     必须手工注入:openapi.json 里没有声明任何 securityScheme,生成的客户端不知道自己需要凭证。
/// </remarks>
public sealed class BearerTokenAuthenticationProvider : IAuthenticationProvider
{
    private readonly ITokenStore _tokens;

    /// <summary>使用指定的凭证存储创建认证提供者。</summary>
    public BearerTokenAuthenticationProvider(ITokenStore tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    /// <inheritdoc />
    public Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var token = _tokens.AccessToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Add("Authorization", $"Bearer {token}");
        }

        return Task.CompletedTask;
    }
}

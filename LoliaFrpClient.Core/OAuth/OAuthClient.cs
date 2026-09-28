using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoliaFrpClient.Core;

/// <summary>
///     OAuth2 公共客户端(Authorization Code + PKCE,无 client_secret)。
///     <para>
///         直接使用 <see cref="HttpClient" /> 而非生成的客户端:令牌端点是标准 OAuth2 端点,
///         返回的是 RFC 6749 定义的字段,与业务 API 的 <c>{code,msg,data}</c> 信封不同构。
///     </para>
/// </summary>
public sealed class OAuthClient
{
    private readonly HttpClient _http;
    private readonly OAuthOptions _options;
    private readonly object _gate = new();
    private PendingAuthorization? _pending;

    /// <summary>创建 OAuth 客户端。</summary>
    /// <param name="options">OAuth 配置。</param>
    /// <param name="httpClient">可选的 HttpClient,便于测试注入。</param>
    public OAuthClient(OAuthOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    ///     生成授权页地址,并把本次 PKCE 的 code_verifier 与 state 暂存起来,
    ///     供随后的 <see cref="ExchangeCodeAsync" /> 使用。
    /// </summary>
    /// <returns>应在浏览器中打开的地址。</returns>
    public string BeginAuthorization()
    {
        var codeVerifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier)));

        lock (_gate)
        {
            _pending = new PendingAuthorization(codeVerifier, state);
        }

        return $"{_options.AuthorizeEndpoint}" +
               $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
               "&response_type=code" +
               $"&scope={Uri.EscapeDataString(_options.Scope)}" +
               $"&redirect_uri={Uri.EscapeDataString(_options.CallbackUri)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&code_challenge={challenge}" +
               "&code_challenge_method=S256";
    }

    /// <summary>用授权码换取令牌。</summary>
    /// <param name="code">回调地址上带回来的 <c>code</c>。</param>
    /// <param name="state">回调地址上带回来的 <c>state</c>,必须与发起时一致。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <exception cref="InvalidOperationException">未先调用 <see cref="BeginAuthorization" />,或 state 校验失败。</exception>
    public async Task<OAuthTokenResponse> ExchangeCodeAsync(string code, string? state, CancellationToken cancellationToken = default)
    {
        PendingAuthorization pending;
        lock (_gate)
        {
            pending = _pending ?? throw new InvalidOperationException("尚未发起授权,请先调用 BeginAuthorization。");
        }

        if (string.IsNullOrEmpty(state) || !string.Equals(state, pending.State, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("state 校验失败,请重新发起授权。");
        }

        var response = await SendTokenRequestAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _options.ClientId,
                ["code"] = code,
                ["code_verifier"] = pending.CodeVerifier,
                ["redirect_uri"] = _options.CallbackUri
            },
            cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            _pending = null;
        }

        return response;
    }

    /// <summary>用刷新令牌换取新的访问令牌。</summary>
    public Task<OAuthTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        SendTokenRequestAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = _options.ClientId
            },
            cancellationToken);

    private async Task<OAuthTokenResponse> SendTokenRequestAsync(
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(parameters);
        using var response = await _http.PostAsync(_options.TokenEndpoint, content, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"令牌请求失败:{(int)response.StatusCode} - {body}");
        }

        return JsonSerializer.Deserialize(body, OAuthJsonContext.Default.OAuthTokenResponse)
               ?? throw new JsonException("令牌响应无法解析。");
    }

    private sealed record PendingAuthorization(string CodeVerifier, string State);
}

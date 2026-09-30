using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoliaFrpClient.Core;

public sealed class OAuthClient
{
    private readonly HttpClient _http;
    private readonly OAuthOptions _options;
    private readonly object _gate = new();
    private PendingAuthorization? _pending;

    public OAuthClient(OAuthOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

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

    public async Task<OAuthTokenResponse> ExchangeCodeAsync(string code, string? state,
        CancellationToken cancellationToken = default)
    {
        PendingAuthorization pending;
        lock (_gate)
        {
            pending = _pending ?? throw new InvalidOperationException("尚未发起授权,请先调用 BeginAuthorization。");
        }

        if (string.IsNullOrEmpty(state) || !string.Equals(state, pending.State, StringComparison.Ordinal))
            throw new InvalidOperationException("state 校验失败,请重新发起授权。");

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

    public Task<OAuthTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return SendTokenRequestAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = _options.ClientId
            },
            cancellationToken);
    }

    private async Task<OAuthTokenResponse> SendTokenRequestAsync(
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(parameters);
        using var response =
            await _http.PostAsync(_options.TokenEndpoint, content, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"令牌请求失败:{(int)response.StatusCode} - {body}");

        return JsonSerializer.Deserialize(body, OAuthJsonContext.Default.OAuthTokenResponse)
               ?? throw new JsonException("令牌响应无法解析。");
    }

    private sealed record PendingAuthorization(string CodeVerifier, string State);
}
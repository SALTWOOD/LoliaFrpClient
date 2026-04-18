using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LoliaFrpClient.Constants;

namespace LoliaFrpClient.Services;

public class OAuthTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("scope")] public string Scope { get; set; } = string.Empty;
}

/// <summary>
/// OAuth Token 服务 (Public Client 模式，集成 PKCE)
/// </summary>
public class OAuthTokenService
{
    private static readonly HttpClient _httpClient = new();

    private sealed class PendingAuthorizationSession
    {
        public required string CodeVerifier { get; init; }
        public required string State { get; init; }
    }

    private static PendingAuthorizationSession? _pendingSession;

    #region PKCE 逻辑

    public static string GetAuthorizationUrl()
    {
        var codeVerifier = GenerateRandomBase64Url(32);
        var state = GenerateRandomBase64Url(32);

        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        var codeChallenge = Base64UrlEncode(challengeBytes);
        var callbackUri = GetCallbackUri();

        _pendingSession = new PendingAuthorizationSession
        {
            CodeVerifier = codeVerifier,
            State = state
        };

        return $"{OAuthConstants.AuthorizeEndpoint}" +
               $"?client_id={Uri.EscapeDataString(OAuthConstants.ClientId)}" +
               $"&response_type={Uri.EscapeDataString(OAuthConstants.ResponseType)}" +
               $"&scope={Uri.EscapeDataString(OAuthConstants.Scope)}" +
               $"&redirect_uri={Uri.EscapeDataString(callbackUri)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&code_challenge={codeChallenge}" +
               $"&code_challenge_method=S256";
    }
    
    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string GenerateRandomBase64Url(int byteCount)
    {
        var randomBytes = new byte[byteCount];
        RandomNumberGenerator.Fill(randomBytes);
        return Base64UrlEncode(randomBytes);
    }

    private static string GetCallbackUri()
    {
        return $"http://localhost:{OAuthConstants.CallbackPort}{OAuthConstants.CallbackPath}";
    }

    #endregion

    #region Token 交换与刷新

    /// <summary>
    /// 使用授权码交换 Access Token (PKCE)
    /// </summary>
    public static async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(string code, string? state)
    {
        var pendingSession = _pendingSession;
        if (pendingSession == null)
        {
            throw new InvalidOperationException("PKCE session is missing. Start authorization again before exchanging the code.");
        }

        if (string.IsNullOrEmpty(state) || !string.Equals(state, pendingSession.State, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("OAuth state validation failed. Start authorization again.");
        }

        var tokenRequest = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "client_id", OAuthConstants.ClientId },
            { "code", code },
            { "code_verifier", pendingSession.CodeVerifier },
            { "redirect_uri", GetCallbackUri() }
        };

        var response = await SendTokenRequestAsync(tokenRequest);

        _pendingSession = null;
        return response;
    }

    /// <summary>
    /// 使用 Refresh Token 刷新令牌
    /// </summary>
    public static async Task<OAuthTokenResponse> RefreshTokenAsync(string refreshToken)
    {
        var tokenRequest = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken },
            { "client_id", OAuthConstants.ClientId }
        };

        return await SendTokenRequestAsync(tokenRequest);
    }

    private static async Task<OAuthTokenResponse> SendTokenRequestAsync(Dictionary<string, string> parameters)
    {
        using var content = new FormUrlEncodedContent(parameters);
        using var response = await _httpClient.PostAsync(OAuthConstants.TokenEndpoint, content);

        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // Log: Token request failed
            throw new Exception($"OAuth operation failed: {response.StatusCode} - {responseContent}");
        }

        var tokenResponse = JsonSerializer.Deserialize(
            responseContent,
            AppJsonContext.Default.OAuthTokenResponse
        );

        return tokenResponse ?? throw new Exception("Failed to deserialize token response");
    }

    #endregion
}

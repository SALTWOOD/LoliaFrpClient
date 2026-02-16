using LoliaFrpClient.Constants;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LoliaFrpClient.Services
{
    /// <summary>
    /// OAuth Token 响应数据
    /// </summary>
    public class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = string.Empty;
    }

    /// <summary>
    /// OAuth Token 服务，用于处理 OAuth token 交换和刷新
    /// </summary>
    public class OAuthTokenService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// 使用授权码交换 access token
        /// </summary>
        public static async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(string code)
        {
            var tokenRequest = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "client_id", OAuthConstants.ClientId },
                { "client_secret", OAuthConstants.ClientSecret },
                { "code", code },
                { "redirect_uri", $"http://localhost:{OAuthConstants.CallbackPort}{OAuthConstants.CallbackPath}" }
            };

            var content = new FormUrlEncodedContent(tokenRequest);
            var response = await _httpClient.PostAsync(OAuthConstants.TokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"获取 token 失败: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            var tokenResponse = JsonSerializer.Deserialize(
                responseContent,
                AppJsonContext.Default.OAuthTokenResponse
            );

            return tokenResponse ?? throw new Exception("解析 token 响应失败");
        }

        /// <summary>
        /// 使用 refresh token 刷新 access token
        /// </summary>
        public static async Task<OAuthTokenResponse> RefreshTokenAsync(string refreshToken)
        {
            var tokenRequest = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken },
                { "client_id", OAuthConstants.ClientId },
                { "client_secret", OAuthConstants.ClientSecret }
            };

            var content = new FormUrlEncodedContent(tokenRequest);
            var response = await _httpClient.PostAsync(OAuthConstants.TokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"刷新 token 失败: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            var tokenResponse = JsonSerializer.Deserialize(
                responseContent,
                AppJsonContext.Default.OAuthTokenResponse
            );

            return tokenResponse ?? throw new Exception("解析 token 响应失败");
        }
    }
}
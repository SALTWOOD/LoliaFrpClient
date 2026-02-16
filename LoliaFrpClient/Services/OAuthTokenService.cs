using LoliaFrpClient.Constants;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LoliaFrpClient.Services
{
    /// <summary>
    /// OAuth Token 服务，用于处理 OAuth token 交换
    /// </summary>
    public class OAuthTokenService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// 使用授权码交换 access token
        /// </summary>
        /// <param name="code">授权码</param>
        /// <returns>Access token</returns>
        public static async Task<string> ExchangeCodeForTokenAsync(string code)
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
            using var jsonDoc = JsonDocument.Parse(responseContent);
            
            var tokenElement = jsonDoc.RootElement.GetProperty("access_token");
            return tokenElement.GetString();
        }
    }
}

using System.Text.Json.Serialization;

namespace LoliaFrpClient.Core;

/// <summary>OAuth2 令牌端点返回的标准字段。</summary>
public sealed class OAuthTokenResponse
{
    /// <summary>访问令牌。</summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>令牌类型,通常为 <c>Bearer</c>。</summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    /// <summary>有效期(秒)。</summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>刷新令牌。</summary>
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>授权范围。</summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}

[JsonSerializable(typeof(OAuthTokenResponse))]
internal sealed partial class OAuthJsonContext : JsonSerializerContext
{
}

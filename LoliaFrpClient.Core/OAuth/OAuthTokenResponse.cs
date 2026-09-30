using System.Text.Json.Serialization;

namespace LoliaFrpClient.Core;

public sealed class OAuthTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")] public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("scope")] public string Scope { get; set; } = string.Empty;
}

[JsonSerializable(typeof(OAuthTokenResponse))]
internal sealed partial class OAuthJsonContext : JsonSerializerContext
{
}
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

/// RFC 6749 §5.2
internal sealed class OAuthErrorResponse
{
    [JsonPropertyName("error")] public string? Error { get; set; }

    [JsonPropertyName("error_description")] public string? ErrorDescription { get; set; }
}

[JsonSerializable(typeof(OAuthTokenResponse))]
[JsonSerializable(typeof(OAuthDeviceCode))]
[JsonSerializable(typeof(OAuthErrorResponse))]
internal sealed partial class OAuthJsonContext : JsonSerializerContext
{
}

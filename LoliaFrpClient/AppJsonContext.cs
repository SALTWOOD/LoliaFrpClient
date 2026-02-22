using System.Collections.Generic;
using System.Text.Json.Serialization;
using LoliaFrpClient.Services;

namespace LoliaFrpClient;

/// <summary>
///     JSON 序列化上下文，适配 AOT 和裁剪
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(OAuthTokenResponse))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(List<GitHubRelease>))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
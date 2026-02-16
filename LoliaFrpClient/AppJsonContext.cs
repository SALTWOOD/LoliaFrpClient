using LoliaFrpClient.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LoliaFrpClient;

/// <summary>
/// JSON 序列化上下文，适配 AOT 和裁剪
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(OAuthTokenResponse))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(List<GitHubRelease>))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
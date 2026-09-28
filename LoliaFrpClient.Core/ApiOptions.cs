namespace LoliaFrpClient.Core;

/// <summary>
///     API 端点配置。
///     <para>
///         必须显式提供 base URL:openapi.json 里的 <c>servers</c> 是空数组,生成的客户端拿不到地址。
///     </para>
/// </summary>
public sealed class ApiOptions
{
    /// <summary>API 根地址,不带尾斜杠。</summary>
    public string BaseUrl { get; init; } = "https://api.lolia.link/api/v1";

    /// <summary>单次请求超时。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>OAuth 相关配置。</summary>
    public OAuthOptions OAuth { get; init; } = new();
}

namespace LoliaFrpClient.Constants;

/// <summary>
///     隧道状态值，对应 API 返回的 <c>status</c> 字段。
/// </summary>
public static class TunnelStatus
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Disabled = "disabled";
}

/// <summary>
///     隧道协议类型，对应 API 的 <c>type</c> 字段与筛选框的 Tag。
/// </summary>
public static class TunnelType
{
    public const string Tcp = "tcp";
    public const string Udp = "udp";
    public const string Http = "http";
    public const string Https = "https";

    /// <summary>「全部」筛选项的 Tag，不作为真实隧道类型使用。</summary>
    public const string All = "all";

    /// <summary>HTTP/HTTPS 需要使用自定义域名。</summary>
    public static bool RequiresCustomDomain(string? type) => type is Http or Https;
}

/// <summary>
///     节点状态值，对应 API 返回的 <c>status</c> 字段。
/// </summary>
public static class NodeStatus
{
    public const string Online = "online";
    public const string Offline = "offline";
    public const string Maintenance = "maintenance";
}

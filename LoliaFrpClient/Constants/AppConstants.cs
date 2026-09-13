namespace LoliaFrpClient.Constants;

/// <summary>
///     应用级端点常量。集中管理便于更换域名与镜像。
/// </summary>
public static class AppConstants
{
    /// <summary>Kiota 客户端使用的 API 根地址。</summary>
    public const string ApiBaseUrl = "https://api.lolia.link/api/v1";

    /// <summary>
    ///     本应用在 GitHub 上的发布仓库，用于客户端自动更新。
    /// </summary>
    public const string ClientReleaseOwner = "SALTWOOD";
    public const string ClientReleaseRepo = "LoliaFrpClient";

    /// <summary>
    ///     frpc 核心的发布仓库，用于下载与更新 frpc 二进制。
    /// </summary>
    public const string FrpcReleaseOwner = "Lolia-FRP";
    public const string FrpcReleaseRepo = "lolia-frp";
}

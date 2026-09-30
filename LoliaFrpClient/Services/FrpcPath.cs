using LoliaFrpClient.Core.Frpc;

namespace LoliaFrpClient.Services;

// 实际使用的 frpc 二进制。
//
// 内置的优先:移动端把 frpc 打进 APK,那条路径是唯一可执行的;
// 桌面端没有内置,<see cref="FrpcLocator.BundledPath" /> 恒为 null,自然回落到用户配置/下载的路径。
internal static class FrpcPath
{
    public static string? Current => FrpcLocator.BundledPath ?? AppSettings.Current.FrpcPath;

    // 内置时,用户不该看到「指定路径 / 安装 / 卸载」——换不了也用不着。
    public static bool IsBundled => FrpcLocator.HasBundled;
}
namespace LoliaFrpClient.Core.Frpc;

/// <summary>
///     随应用一起打包的 frpc。
/// </summary>
/// <remarks>
///     <para>
///         Android 10(API 29)起禁止执行应用可写目录里的文件(W^X),所以移动端走不了
///         「下载到本地再运行」那条路:二进制以 <c>libfrpc.so</c> 的名字打进 APK,
///         由安装器解压到 native lib 目录——只有那里允许执行。
///     </para>
///     <para>
///         代价是内置 frpc 的版本与 APK 版本绑定,不能像桌面端那样在应用内单独更新它。
///     </para>
/// </remarks>
public static class FrpcLocator
{
    private static string? _bundledPath;

    /// <summary>由平台头在启动时调用,传入内置二进制的绝对路径。</summary>
    public static void UseBundled(string? path)
    {
        _bundledPath = path;
    }

    /// <summary>
    ///     frpc 子进程的工作目录。
    /// </summary>
    /// <remarks>
    ///     桌面端留空,沿用进程当前目录。Android 上必须设成应用私有目录:
    ///     应用的 cwd 是 <c>/</c>,而根目录只读,frpc 往 <c>./autotls-cache</c>
    ///     写 ACME 缓存时会直接失败并退出(read-only filesystem)。
    /// </remarks>
    public static string? WorkingDirectory { get; set; }

    /// <summary>内置 frpc 的路径。没有内置、或文件已被移除时为 <c>null</c>。</summary>
    public static string? BundledPath =>
        !string.IsNullOrWhiteSpace(_bundledPath) && File.Exists(_bundledPath) ? _bundledPath : null;

    public static bool HasBundled => BundledPath is not null;
}
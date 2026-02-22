namespace LoliaFrpClient.Services;

/// <summary>
///     服务定位器，用于管理应用程序中的服务实例
/// </summary>
public static class ServiceLocator
{
    private static FrpcManager? _frpcManager;

    /// <summary>
    ///     获取 FrpcManager 实例
    /// </summary>
    public static FrpcManager FrpcManager
    {
        get
        {
            _frpcManager ??= new FrpcManager();
            return _frpcManager;
        }
    }

    /// <summary>
    ///     重置所有服务实例（主要用于测试）
    /// </summary>
    public static void Reset()
    {
        _frpcManager = null;
    }
}
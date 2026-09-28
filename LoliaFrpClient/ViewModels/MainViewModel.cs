using CommunityToolkit.Mvvm.ComponentModel;

namespace LoliaFrpClient.ViewModels;

/// <summary>
/// 主窗口的 ViewModel。只负责当前显示哪一页,不含任何业务逻辑。
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    public MainViewModel()
    {
        // 默认落在总览页。
        CurrentPage = new UserInfoViewModel();
    }

    /// <summary>按导航项的 Tag 切换页面。</summary>
    public void Navigate(string? tag)
    {
        CurrentPage = tag switch
        {
            "tunnels" => new TunnelListViewModel(),
            "frpc" => new FrpcManagerViewModel(),
            "settings" => new SettingsViewModel(),
            _ => new UserInfoViewModel()
        };
    }
}

using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LoliaFrpClient.ViewModels;

/// <summary>
/// 主窗口的 ViewModel。只负责当前显示哪一页,不含任何业务逻辑。
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    // 页面实例长期持有,而不是每次导航 new 一个。
    // 登录态、已加载的隧道列表、正在进行的请求都挂在这些 ViewModel 上,
    // 每次重建会让它们全部丢失——切走再切回来就变回一片空白。
    private readonly UserInfoViewModel _dashboard = new();
    private readonly TunnelListViewModel _tunnels = new();
    private readonly FrpcManagerViewModel _frpc = new();
    private readonly SettingsViewModel _settings = new();

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    public MainViewModel()
    {
        // 默认落在总览页。
        CurrentPage = _dashboard;
    }

    /// <summary>按导航项的 Tag 切换页面,并让新页面重新取数。</summary>
    public async Task NavigateAsync(string? tag)
    {
        CurrentPage = tag switch
        {
            "tunnels" => _tunnels,
            "frpc" => _frpc,
            "settings" => _settings,
            _ => _dashboard
        };

        await CurrentPage.ActivateAsync();
    }
}

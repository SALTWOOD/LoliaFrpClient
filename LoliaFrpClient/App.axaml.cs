using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LoliaFrpClient.Core;
using LoliaFrpClient.ViewModels;
using LoliaFrpClient.Views;

namespace LoliaFrpClient;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 会话在这里建好,而不是等第一次访问 ApiSession.Current 时懒加载:
        // 令牌文件要在启动时读入,401 兜底也要在这里挂上。
        var session = new ApiSession();

        // 刷新令牌也失效时(长期未开机、服务端吊销),清掉本地凭证回到未登录态。
        // 不清的话界面会停在「已登录」但每个请求都 401。
        session.UnauthorizedDetected += session.SignOut;
        ApiSession.Initialize(session);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };

            desktop.Exit += (_, _) => session.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

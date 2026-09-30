using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LoliaFrpClient.Core;
using LoliaFrpClient.Services;
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
        var session = new ApiSession();

        session.UnauthorizedDetected += session.SignOut;
        ApiSession.Initialize(session);

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };

                desktop.Exit += (_, _) => Shutdown(session);
                break;

            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new MobileShellView
                {
                    DataContext = new MainViewModel()
                };
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void Shutdown(ApiSession session)
    {
        // frpc runs as a child process and does not exit with us. Leaving it behind keeps
        // the tunnel occupied and makes the next start fight the orphan.
        FrpcProcessManager.Current.Dispose();
        session.Dispose();
    }
}
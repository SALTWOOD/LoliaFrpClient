using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoliaFrpClient.Core;
using LoliaFrpClient.Core.Frpc;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.ViewModels;

/// <summary>
/// 设置页。账户区块已接入 OAuth 登录;外观、frpc 核心、关于仍是占位。
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel()
    {
        SetAccount(("登录状态", "未登录"));
    }

    /// <summary>账户区块。登录态变化后整块重建。</summary>
    public ObservableCollection<SettingsEntry> Account { get; } = [];

    /// <summary>占位:外观区块。</summary>
    public IReadOnlyList<SettingsEntry> Appearance { get; } =
    [
        new("主题", "跟随系统"),
        new("语言", "简体中文")
    ];

    // Rebuilt on every entry to the page and after install/uninstall.
    public ObservableCollection<SettingsEntry> FrpcCore { get; } = [];

    /// <summary>占位:关于区块。</summary>
    public IReadOnlyList<SettingsEntry> About { get; } =
    [
        new("客户端版本", "—"),
        new("最后检查更新", "从未")
    ];

    /// <summary>是否有请求在途。驱动按钮禁用与进度指示。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    [NotifyCanExecuteChangedFor(nameof(SignOutCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>当前是否持有凭证。</summary>
    [ObservableProperty]
    public partial bool IsSignedIn { get; set; }

    /// <summary>操作结果提示。为空时不显示。</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public override Task ActivateAsync()
    {
        RefreshFrpcCore();
        return RefreshAccountAsync();
    }

    public event Action? InstallFrpcRequested;

    public event Action? PickFrpcPathRequested;

    public event Action? OpenFrpcManagerRequested;

    // 内置 frpc 时「指定路径 / 安装 / 卸载」都无从谈起,页面据此把这三个按钮藏掉。
    public bool CanManageFrpc => !FrpcPath.IsBundled;

    public void RefreshFrpcCore()
    {
        var settings = AppSettings.Current;
        var path = FrpcPath.Current;
        var bundled = FrpcPath.IsBundled;
        var installed = !string.IsNullOrWhiteSpace(path) && File.Exists(path);

        FrpcCore.Clear();
        FrpcCore.Add(new SettingsEntry(
            "本地版本",
            installed ? settings.FrpcVersion ?? (bundled ? "内置" : "未知") : "未安装"));
        FrpcCore.Add(new SettingsEntry("安装路径", bundled ? "随应用打包" : installed ? path! : "—"));

        // 内置的 frpc 不走下载,「下载源」对它没有意义。
        if (!bundled) FrpcCore.Add(new SettingsEntry("下载源", settings.UseDownloadMirror ? "镜像加速" : "GitHub 直连"));
    }

    [RelayCommand]
    private void InstallFrpc()
    {
        InstallFrpcRequested?.Invoke();
    }

    [RelayCommand]
    private void PickFrpcPath()
    {
        PickFrpcPathRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenFrpcManager()
    {
        OpenFrpcManagerRequested?.Invoke();
    }

    // Applies a picked frpc path and probes its version immediately. The settings page is the
    // only entry point on purpose: offering it on the manager page invites swapping the binary
    // while tunnels are live, which affects only the next start and is hard to explain.
    public async Task SetFrpcPathAsync(string path)
    {
        AppSettings.Current.FrpcPath = path;
        AppSettings.Current.FrpcVersion = null;
        AppSettings.Current.Save();

        var version = await FrpcVersionProbe.TryReadAsync(path).ConfigureAwait(true);
        if (version is not null)
        {
            AppSettings.Current.FrpcVersion = version;
            AppSettings.Current.Save();
        }

        RefreshFrpcCore();
        StatusMessage = $"已指定 frpc:{path}";
    }

    // Only deletes files inside the managed directory. A user-supplied frpc is outside this
    // app's authority, so refuse rather than delete it.
    [RelayCommand]
    private async Task UninstallFrpcAsync()
    {
        if (FrpcPath.IsBundled)
        {
            StatusMessage = "frpc 随应用打包,不能单独卸载。";
            return;
        }

        var path = AppSettings.Current.FrpcPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "本机没有配置 frpc。";
            return;
        }

        if (!FrpcInstaller.IsManagedPath(path))
        {
            StatusMessage = "该 frpc 由你自行指定,本应用不会删除它。";
            return;
        }

        // 运行中的 exe 删不掉,先把隧道停掉。
        await Task.Run(() => FrpcProcessManager.Current.StopAll()).ConfigureAwait(true);

        var removed = await Task.Run(() => TryDelete(path)).ConfigureAwait(true);

        AppSettings.Current.FrpcPath = null;
        AppSettings.Current.FrpcVersion = null;
        AppSettings.Current.Save();

        RefreshFrpcCore();
        StatusMessage = removed ? "已卸载 frpc。" : "文件删除失败,可能仍被占用。";
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task SignInAsync()
    {
        IsBusy = true;
        StatusMessage = "已打开浏览器,请在其中完成授权。";
        try
        {
            var result = await OAuthLogin.SignInAsync(BrowserLauncher.OpenAsync);
            StatusMessage = result.IsSuccess ? "登录成功。" : $"登录失败:{result.Msg}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"登录失败:{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAccountAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task SignOutAsync()
    {
        IsBusy = true;
        try
        {
            // 先让服务端撤销会话,再清本地凭证——顺序反了就带着空 token 去请求了。
            await new User().LogoutAsync();
        }
        catch (Exception)
        {
            // 服务端撤销失败不影响本地登出,下次登录会覆盖旧会话。
        }
        finally
        {
            ApiSession.Current.SignOut();
            IsBusy = false;
            StatusMessage = "已登出。";
        }

        await RefreshAccountAsync();
    }

    private bool CanRun()
    {
        return !IsBusy;
    }

    /// <summary>按当前会话重建账户区块。进入设置页与每次登录/登出后都会走一遍。</summary>
    private async Task RefreshAccountAsync()
    {
        var session = ApiSession.Current;
        IsSignedIn = session.IsAuthenticated;

        var origin = session.Tokens.Origin switch
        {
            TokenOrigin.OAuth2 => "OAuth2 访问令牌",
            TokenOrigin.UserJwt => "用户 JWT",
            _ => "—"
        };

        if (!session.IsAuthenticated)
        {
            SetAccount(
                ("登录状态", "未登录"),
                ("API 地址", session.Options.BaseUrl),
                ("Token 来源", origin));
            return;
        }

        try
        {
            var result = await User.MeAsync();

            if (result is { IsSuccess: true, Data: { } me })
            {
                SetAccount(
                    ("用户名", me.Username ?? "—"),
                    ("邮箱", me.Email ?? "—"),
                    ("API 地址", session.Options.BaseUrl),
                    ("Token 来源", origin));
            }
            else
            {
                // 凭证有效但资料没取到(例如网络断了),不该把界面退化成未登录。
                SetAccount(
                    ("登录状态", "已登录"),
                    ("API 地址", session.Options.BaseUrl),
                    ("Token 来源", origin));
                StatusMessage = result.Msg;
            }
        }
        catch (Exception ex)
        {
            SetAccount(
                ("登录状态", "已登录"),
                ("API 地址", session.Options.BaseUrl),
                ("Token 来源", origin));
            StatusMessage = ex.Message;
        }
    }

    private void SetAccount(params (string Label, string Value)[] rows)
    {
        Account.Clear();
        foreach (var (label, value) in rows) Account.Add(new SettingsEntry(label, value));
    }
}

/// <summary>设置页中的一行「标签 / 值」。</summary>
public sealed record SettingsEntry(string Label, string Value);
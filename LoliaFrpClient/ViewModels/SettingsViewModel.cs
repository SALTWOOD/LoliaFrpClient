using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoliaFrpClient.Core;
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

    /// <summary>占位:frpc 核心区块。</summary>
    public IReadOnlyList<SettingsEntry> FrpcCore { get; } =
    [
        new("本地版本", "未安装"),
        new("下载源", "官方镜像")
    ];

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

    /// <inheritdoc />
    public override Task ActivateAsync() => RefreshAccountAsync();

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

    private bool CanRun() => !IsBusy;

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
        foreach (var (label, value) in rows)
        {
            Account.Add(new SettingsEntry(label, value));
        }
    }
}

/// <summary>设置页中的一行「标签 / 值」。</summary>
public sealed record SettingsEntry(string Label, string Value);

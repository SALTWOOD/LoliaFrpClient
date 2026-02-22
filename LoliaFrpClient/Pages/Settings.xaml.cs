using System;
using System.Threading.Tasks;
using System.Web;
using Windows.System;
using LoliaFrpClient.Constants;
using LoliaFrpClient.Controls;
using LoliaFrpClient.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LoliaFrpClient.Pages;

/// <summary>
///     Settings page with theme toggle and login functionality
/// </summary>
public sealed partial class Settings : Page
{
    private readonly ApiClientProvider _apiClientProvider = ApiClientProvider.Instance;
    private readonly FrpcManager _frpcManager = new();
    private readonly SettingsStorage _settings = SettingsStorage.Instance;
    private ClientUpdateResult? _clientUpdateResult;
    private GitHubRelease? _latestFrpcRelease;

    public Settings()
    {
        InitializeComponent();
        DataContext = this;
        UpdateLoginStatus();
        InitializeMirrorSettings();
        InitializeClientUpdateSettings();
        InitializeFrpcManagement();
    }

    /// <summary>
    ///     初始化镜像源设置
    /// </summary>
    private void InitializeMirrorSettings()
    {
        var mirrorType = _settings.GitHubMirrorType;

        // 根据保存的设置选择对应的 RadioButton
        foreach (var item in GitHubMirrorRadioButtons.Items)
            if (item is RadioButton radioButton && radioButton.Tag is string tag)
                if (int.Parse(tag) == mirrorType)
                {
                    radioButton.IsChecked = true;
                    break;
                }
    }

    /// <summary>
    ///     初始化客户端更新设置
    /// </summary>
    private void InitializeClientUpdateSettings()
    {
        // 设置自动检查开关状态
        AutoCheckUpdateToggle.IsOn = _settings.AutoCheckClientUpdate;

        // 显示当前客户端版本
        ClientCurrentVersionText.Text = ClientUpdateService.GetCurrentVersion();
    }

    /// <summary>
    ///     自动检查更新开关变更事件
    /// </summary>
    private void AutoCheckUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _settings.AutoCheckClientUpdate = AutoCheckUpdateToggle.IsOn;
    }

    /// <summary>
    ///     手动检查客户端更新按钮点击事件
    /// </summary>
    private async void CheckClientUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        await CheckClientUpdateAsync();
    }

    /// <summary>
    ///     检查客户端更新
    /// </summary>
    private async Task CheckClientUpdateAsync()
    {
        try
        {
            ClientLatestVersionText.Text = "检查中...";
            ClientUpdateStatusText.Visibility = Visibility.Visible;
            ClientUpdateStatusText.Text = "正在检查更新...";
            ClientUpdateButton.Visibility = Visibility.Collapsed;

            _clientUpdateResult = await ClientUpdateService.CheckForUpdateAsync();

            if (string.IsNullOrEmpty(_clientUpdateResult.LatestVersion)) ClientCurrentVersionText.Text = "检查失败";
            else ClientLatestVersionText.Text = _clientUpdateResult.LatestVersion;

            if (_clientUpdateResult.HasUpdate)
            {
                ClientUpdateStatusText.Text = $"发现新版本 {_clientUpdateResult.LatestVersion}";
                ClientUpdateButton.Visibility = Visibility.Visible;
            }
            else
            {
                ClientUpdateStatusText.Text = "已是最新版本";
            }
        }
        catch (Exception ex)
        {
            ClientLatestVersionText.Text = "检查失败";
            ClientUpdateStatusText.Text = $"检查更新失败: {ex.Message}";
        }
    }

    /// <summary>
    ///     前往下载客户端更新按钮点击事件
    /// </summary>
    private async void ClientUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_clientUpdateResult != null && !string.IsNullOrEmpty(_clientUpdateResult.ReleaseUrl))
        {
            var uri = new Uri(_clientUpdateResult.ReleaseUrl);
            await Launcher.LaunchUriAsync(uri);
        }
    }

    /// <summary>
    ///     镜像源选择变更事件
    /// </summary>
    private void GitHubMirrorRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GitHubMirrorRadioButtons.SelectedItem is RadioButton radioButton && radioButton.Tag is string tag)
            _settings.GitHubMirrorType = int.Parse(tag);
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        // 使用 UriBuilder 构建 OAuth 授权 URL
        var callbackUrl = OAuthCallbackService.GetCallbackUrl();
        var uriBuilder = new UriBuilder(OAuthConstants.AuthorizeEndpoint);

        // 添加查询参数
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["client_id"] = OAuthConstants.ClientId;
        query["redirect_uri"] = callbackUrl;
        query["response_type"] = OAuthConstants.ResponseType;
        query["scope"] = OAuthConstants.Scope;
        uriBuilder.Query = query.ToString();

        // 创建并显示登录弹窗
        var loginDialog = new LoginDialog(uriBuilder.ToString(), OnCallbackReceived);
        loginDialog.XamlRoot = XamlRoot;
        await loginDialog.ShowAsync();
    }

    /// <summary>
    ///     接收到 OAuth 回调时的处理
    /// </summary>
    private async void OnCallbackReceived(OAuthCallbackService.OAuthCallbackResult result)
    {
        if (result.Error != null)
        {
            // 显示错误信息
            _ = ShowDialogAsync(
                $"授权失败: {result.Error}{(string.IsNullOrEmpty(result.ErrorDescription) ? "" : $" - {result.ErrorDescription}")}");
            return;
        }

        if (result.Code != null)
            try
            {
                // 使用 code 交换 access token
                var tokenResponse = await OAuthTokenService.ExchangeCodeForTokenAsync(result.Code);

                // 存储 OAuthToken
                _settings.OAuthToken = tokenResponse.AccessToken;

                // 存储 refresh token
                _settings.RefreshToken = tokenResponse.RefreshToken;

                // 重新初始化 API 客户端
                _apiClientProvider.ReinitializeClient();

                // 更新登录状态
                UpdateLoginStatus();

                // 显示成功消息
                _ = ShowDialogAsync("登录成功！");
            }
            catch (Exception ex)
            {
                _ = ShowDialogAsync($"获取 token 失败: {ex.Message}");
            }
    }

    /// <summary>
    ///     更新登录状态显示
    /// </summary>
    private void UpdateLoginStatus()
    {
        var isLoggedIn = !string.IsNullOrEmpty(_settings.OAuthToken);

        // 更新登录和退出登录按钮状态
        LoginButton.Visibility = isLoggedIn ? Visibility.Collapsed : Visibility.Visible;
        LogoutButton.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;

        // 更新登录状态卡片
        LoginStatusCard.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
        LoginStatusText.Text = isLoggedIn ? "已登录" : "未登录";
    }

    /// <summary>
    ///     退出登录按钮点击事件
    /// </summary>
    private async void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        // 清除 token
        _settings.OAuthToken = null;

        // 重新初始化 API 客户端
        _apiClientProvider.ReinitializeClient();

        // 更新登录状态
        UpdateLoginStatus();

        // 显示成功消息
        await ShowDialogAsync("已退出登录");
    }

    private async Task ShowDialogAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "提示",
            Content = message,
            CloseButtonText = "确定",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

    #region Frpc Management

    /// <summary>
    ///     初始化 frpc 管理
    /// </summary>
    private async void InitializeFrpcManagement()
    {
        UpdateFrpcStatus();
        await RefreshLatestVersionAsync();
    }

    /// <summary>
    ///     更新 frpc 状态显示
    /// </summary>
    private void UpdateFrpcStatus()
    {
        // 更新当前版本
        CurrentVersionText.Text = _frpcManager.InstalledVersion ?? "未安装";

        // 更新安装状态
        var installStatus = _frpcManager.GetInstallStatus(_latestFrpcRelease?.TagName);
        InstallStatusText.Text = installStatus switch
        {
            FrpcInstallStatus.NotInstalled => "未安装",
            FrpcInstallStatus.Installed => "已安装",
            FrpcInstallStatus.Outdated => "需要更新",
            _ => "未知"
        };

        // 更新进程状态
        ProcessStatusText.Text = _frpcManager.IsAnyProcessRunning ? "运行中" : "未运行";

        // 更新按钮状态
        UpdateFrpcButtons(installStatus);
    }

    /// <summary>
    ///     更新 frpc 按钮状态
    /// </summary>
    private void UpdateFrpcButtons(FrpcInstallStatus installStatus)
    {
        InstallButton.IsEnabled = installStatus == FrpcInstallStatus.NotInstalled;
        UpdateButton.IsEnabled = installStatus == FrpcInstallStatus.Outdated;
        UninstallButton.IsEnabled = installStatus != FrpcInstallStatus.NotInstalled;
    }

    /// <summary>
    ///     刷新最新版本
    /// </summary>
    private async void RefreshVersionButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshLatestVersionAsync();
    }

    /// <summary>
    ///     刷新最新版本
    /// </summary>
    private async Task RefreshLatestVersionAsync()
    {
        try
        {
            LatestVersionText.Text = "检查中...";

            // 首先尝试从 API 获取版本标签
            var versionTag = await GitHubReleaseService.GetLatestVersionFromApiAsync();

            _latestFrpcRelease = await GitHubReleaseService.GetLatestReleaseAsync("Lolia-FRP", "lolia-frp");
            LatestVersionText.Text = versionTag;

            UpdateFrpcStatus();
        }
        catch (Exception ex)
        {
            LatestVersionText.Text = "获取失败";
            _ = ShowDialogAsync($"获取最新版本失败: {ex.Message}");
        }
    }

    /// <summary>
    ///     安装 frpc
    /// </summary>
    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_latestFrpcRelease == null)
        {
            _ = ShowDialogAsync("请先刷新版本信息");
            return;
        }

        var downloadUrl = GitHubReleaseService.GetDownloadUrlForPlatform(_latestFrpcRelease);
        if (string.IsNullOrEmpty(downloadUrl))
        {
            _ = ShowDialogAsync("找不到适用于当前平台的下载链接");
            return;
        }

        await DownloadAndInstallFrpcAsync(downloadUrl, _latestFrpcRelease.TagName);
    }

    /// <summary>
    ///     更新 frpc
    /// </summary>
    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_latestFrpcRelease == null)
        {
            _ = ShowDialogAsync("请先刷新版本信息");
            return;
        }

        var downloadUrl = GitHubReleaseService.GetDownloadUrlForPlatform(_latestFrpcRelease);
        if (string.IsNullOrEmpty(downloadUrl))
        {
            _ = ShowDialogAsync("找不到适用于当前平台的下载链接");
            return;
        }

        await DownloadAndInstallFrpcAsync(downloadUrl, _latestFrpcRelease.TagName, true);
    }

    /// <summary>
    ///     下载并安装 frpc
    /// </summary>
    private async Task DownloadAndInstallFrpcAsync(string downloadUrl, string version, bool isUpdate = false)
    {
        try
        {
            // 显示进度条
            DownloadProgressBar.Visibility = Visibility.Visible;
            ProgressText.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;

            // 创建进度报告
            var progress = new Progress<double>(value =>
            {
                DownloadProgressBar.Value = value * 100;
                ProgressText.Text = $"下载中... {value * 100:F0}%";
            });

            // 下载 frpc
            ProgressText.Text = "下载中...";
            var downloadPath = await _frpcManager.DownloadFrpcAsync(downloadUrl, progress);

            // 安装 frpc
            ProgressText.Text = "安装中...";
            var success = isUpdate
                ? await _frpcManager.UpdateFrpcAsync(downloadPath, version, progress)
                : await _frpcManager.InstallFrpcAsync(downloadPath, version);

            if (success)
            {
                UpdateFrpcStatus();
                _ = ShowDialogAsync(isUpdate ? "更新成功！" : "安装成功！");
            }
            else
            {
                _ = ShowDialogAsync(isUpdate ? "更新失败" : "安装失败");
            }
        }
        catch (Exception ex)
        {
            _ = ShowDialogAsync($"{(isUpdate ? "更新" : "安装")}失败: {ex.Message}");
        }
        finally
        {
            // 隐藏进度条
            DownloadProgressBar.Visibility = Visibility.Collapsed;
            ProgressText.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    ///     卸载 frpc
    /// </summary>
    private async void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await ShowConfirmDialogAsync("确定要卸载 frpc 吗？");
            if (result == ContentDialogResult.Primary)
            {
                _frpcManager.UninstallFrpc();
                UpdateFrpcStatus();
                _ = ShowDialogAsync("卸载成功！");
            }
        }
        catch (Exception ex)
        {
            _ = ShowDialogAsync($"卸载失败: {ex.Message}");
        }
    }

    /// <summary>
    ///     显示确认对话框
    /// </summary>
    private async Task<ContentDialogResult> ShowConfirmDialogAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "确认",
            Content = message,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync();
    }

    #endregion
}
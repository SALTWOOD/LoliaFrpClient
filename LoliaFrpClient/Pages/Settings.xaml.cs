using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using LoliaFrpClient.Controls;
using LoliaFrpClient.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LoliaFrpClient.Pages;

public sealed partial class Settings : Page
{
    private readonly SettingsStorage _settings = SettingsStorage.Instance;
    private readonly FrpcManager _frpcManager = new();
    private ClientUpdateResult? _updateResult;
    private GitHubRelease? _latestFrpcRelease;

    public Settings()
    {
        InitializeComponent();
        LoadSettings();
        InitializeFrpcManagement();
    }

    private void LoadSettings()
    {
        // Login status
        UpdateLoginStatus();

        // Client Update
        AutoCheckUpdateToggle.IsOn = _settings.AutoCheckClientUpdate;
        ClientCurrentVersionText.Text = ClientUpdateService.GetCurrentVersion();

        // Download Mirror Template
        var currentTemplate = _settings.DownloadUrlTemplate;
        CustomUrlTemplateBox.Text = currentTemplate;

        // Try to match RadioButtons with current template
        var matched = GitHubMirrorRadioButtons.Items
            .OfType<RadioButton>()
            .FirstOrDefault(rb => rb.Tag?.ToString() == currentTemplate);

        if (matched != null) matched.IsChecked = true;
    }

    private void UpdateLoginStatus()
    {
        bool isLoggedIn = !string.IsNullOrEmpty(_settings.OAuthToken);
        LoginButton.Visibility = isLoggedIn ? Visibility.Collapsed : Visibility.Visible;
        LogoutButton.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
        LoginStatusText.Text = isLoggedIn ? "已登录" : "未登录账户";
    }

    #region Events - Client Update

    private void AutoCheckUpdateToggle_Toggled(object sender, RoutedEventArgs e) => 
        _settings.AutoCheckClientUpdate = AutoCheckUpdateToggle.IsOn;

    private async void CheckClientUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        ClientLatestVersionText.Text = "检查中...";
        _updateResult = await ClientUpdateService.CheckForUpdateAsync();
        
        ClientLatestVersionText.Text = _updateResult.LatestVersion;
        ClientUpdateInfoBar.IsOpen = _updateResult.HasUpdate;
        ClientUpdateInfoBar.Message = _updateResult.HasUpdate ? $"发现新版本 {_updateResult.LatestVersion}" : "当前已是最新版本";
        ClientUpdateInfoBar.Severity = _updateResult.HasUpdate ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
    }

    private async void ClientUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_updateResult?.ReleaseUrl))
            await Launcher.LaunchUriAsync(new Uri(_updateResult.ReleaseUrl));
    }

    #endregion

    #region Events - Mirrors

    private void GitHubMirrorRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GitHubMirrorRadioButtons.SelectedItem is RadioButton rb && rb.Tag is string template)
        {
            _settings.DownloadUrlTemplate = template;
            CustomUrlTemplateBox.Text = template;
        }
    }

    private void CustomUrlTemplateBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // User manually edited the template
        if (!string.IsNullOrWhiteSpace(CustomUrlTemplateBox.Text))
            _settings.DownloadUrlTemplate = CustomUrlTemplateBox.Text;
    }

    #endregion

    #region Events - OAuth

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var url = OAuthTokenService.GetAuthorizationUrl();

        var loginDialog = new LoginDialog(url, OnCallbackReceived);
        await DialogManager.Instance.ShowDialogAsync(loginDialog);
    }

    private async void OnCallbackReceived(OAuthCallbackService.OAuthCallbackResult result)
    {
        if (result.Error != null)
        {
            await ShowMsg($"授权失败: {result.Error}");
            return;
        }

        if (string.IsNullOrEmpty(result.Code))
        {
            await ShowMsg("授权失败: 回调中缺少授权码");
            return;
        }

        try
        {
            var tokenResponse = await OAuthTokenService.ExchangeCodeForTokenAsync(result.Code, result.State);
            _settings.OAuthToken = tokenResponse.AccessToken;
            _settings.RefreshToken = tokenResponse.RefreshToken;
            
            ApiClientProvider.Instance.ReinitializeClient();
            UpdateLoginStatus();
            await ShowMsg("登录成功！");
        }
        catch (Exception ex) { await ShowMsg($"Token 交换失败: {ex.Message}"); }
    }

    private async void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.OAuthToken = null;
        ApiClientProvider.Instance.ReinitializeClient();
        UpdateLoginStatus();
        await ShowMsg("已退出登录");
    }

    #endregion

    #region Frpc Management

    private async void InitializeFrpcManagement()
    {
        UpdateFrpcStatus();
        await RefreshLatestVersionAsync();
    }

    private void UpdateFrpcStatus()
    {
        CurrentVersionText.Text = _frpcManager.InstalledVersion ?? "未安装";
        var status = _frpcManager.GetInstallStatus(_latestFrpcRelease?.TagName);
        
        InstallStatusText.Text = status switch {
            FrpcInstallStatus.NotInstalled => "未安装",
            FrpcInstallStatus.Installed => "已安装",
            FrpcInstallStatus.Outdated => "需要更新",
            _ => "未知"
        };
        ProcessStatusText.Text = _frpcManager.IsAnyProcessRunning ? "运行中" : "未运行";
        
        InstallButton.IsEnabled = status == FrpcInstallStatus.NotInstalled;
        UpdateButton.IsEnabled = status == FrpcInstallStatus.Outdated;
        UninstallButton.IsEnabled = status != FrpcInstallStatus.NotInstalled;
    }

    private async void RefreshVersionButton_Click(object sender, RoutedEventArgs e) => await RefreshLatestVersionAsync();

    private async Task RefreshLatestVersionAsync()
    {
        try
        {
            LatestVersionText.Text = "检查中...";
            _latestFrpcRelease = await GitHubReleaseService.GetLatestReleaseAsync("Lolia-FRP", "lolia-frp");
            LatestVersionText.Text = _latestFrpcRelease?.TagName ?? "获取失败";
            UpdateFrpcStatus();
        }
        catch (Exception ex) { await ShowMsg($"获取版本失败: {ex.Message}"); }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e) => await HandleFrpcAction(false);
    private async void UpdateButton_Click(object sender, RoutedEventArgs e) => await HandleFrpcAction(true);

    private async Task HandleFrpcAction(bool isUpdate)
    {
        if (_latestFrpcRelease == null) return;

        var url = GitHubReleaseService.GetDownloadUrl(_latestFrpcRelease, AssetType.Frpc);
        if (url == null) { await ShowMsg("无适用当前平台的包"); return; }

        try
        {
            DownloadProgressBar.Visibility = ProgressText.Visibility = Visibility.Visible;
            var progress = new Progress<double>(v => {
                DownloadProgressBar.Value = v * 100;
                ProgressText.Text = $"进度: {v:P0}";
            });

            var success = await _frpcManager.InstallAsync(url, _latestFrpcRelease.TagName, progress);
            if (success) UpdateFrpcStatus();
            await ShowMsg(success ? (isUpdate ? "更新成功" : "安装成功") : "操作失败");
        }
        catch (Exception ex) { await ShowMsg($"错误: {ex.Message}"); }
        finally { DownloadProgressBar.Visibility = ProgressText.Visibility = Visibility.Collapsed; }
    }

    private async void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (await DialogManager.Instance.ShowConfirmAsync("确认", "确定卸载 frpc 吗？") == ContentDialogResult.Primary)
        {
            _frpcManager.UninstallFrpc();
            UpdateFrpcStatus();
        }
    }

    #endregion

    private Task ShowMsg(string msg) => DialogManager.Instance.ShowMessageAsync("提示", msg);
}

using System;
using System.Threading.Tasks;
using Windows.System;
using LoliaFrpClient.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LoliaFrpClient.Controls;

/// <summary>
///     登录弹窗
/// </summary>
public sealed partial class LoginDialog : ContentDialog
{
    private readonly string _authorizeUrl;
    private readonly OAuthCallbackService _callbackService;
    private readonly Action<OAuthCallbackService.OAuthCallbackResult> _onCallbackReceived;
    private bool _isAuthorized;

    /// <summary>
    ///     构造函数
    /// </summary>
    /// <param name="authorizeUrl">授权 URL</param>
    /// <param name="onCallbackReceived">接收到回调时的回调</param>
    public LoginDialog(string authorizeUrl, Action<OAuthCallbackService.OAuthCallbackResult> onCallbackReceived)
    {
        InitializeComponent();
        _authorizeUrl = authorizeUrl;
        _onCallbackReceived = onCallbackReceived;
        _callbackService = new OAuthCallbackService();

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    /// <summary>
    ///     弹窗加载时启动监听服务并打开授权页面
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 启动回调监听服务
            await _callbackService.StartAsync();

            // 订阅授权完成事件
            _callbackService.AuthorizationCompleted += OnAuthorizationCompleted;

            // 打开授权页面
            await OpenAuthorizePageAsync();
        }
        catch (Exception ex)
        {
            // 显示错误信息
            await ShowErrorDialogAsync($"启动登录服务失败: {ex.Message}");
            Hide();
        }
    }

    /// <summary>
    ///     弹窗关闭时停止监听服务
    /// </summary>
    private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        // 如果已经授权成功，不阻止关闭
        if (_isAuthorized) return;

        // 如果用户点击取消，停止监听服务
        _callbackService.AuthorizationCompleted -= OnAuthorizationCompleted;
        _callbackService.Stop();
    }

    /// <summary>
    ///     授权完成事件处理
    /// </summary>
    private void OnAuthorizationCompleted(object? sender, OAuthCallbackService.OAuthCallbackResult result)
    {
        _isAuthorized = true;

        // 在 UI 线程上执行
        DispatcherQueue.TryEnqueue(() =>
        {
            // 调用回调函数
            _onCallbackReceived?.Invoke(result);

            // 停止监听服务
            _callbackService.AuthorizationCompleted -= OnAuthorizationCompleted;
            _callbackService.Stop();

            // 关闭弹窗
            Hide();
        });
    }

    /// <summary>
    ///     重新打开按钮点击事件
    /// </summary>
    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        await OpenAuthorizePageAsync();
    }

    /// <summary>
    ///     取消按钮点击事件
    /// </summary>
    private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 停止监听服务
        _callbackService.AuthorizationCompleted -= OnAuthorizationCompleted;
        _callbackService.Stop();
    }

    /// <summary>
    ///     打开授权页面
    /// </summary>
    private async Task OpenAuthorizePageAsync()
    {
        try
        {
            var uri = new Uri(_authorizeUrl);
            var options = new LauncherOptions
            {
                TreatAsUntrusted = false
            };
            await Launcher.LaunchUriAsync(uri, options);
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync($"无法打开授权页面: {ex.Message}");
        }
    }

    /// <summary>
    ///     显示错误对话框
    /// </summary>
    private async Task ShowErrorDialogAsync(string message)
    {
        var errorDialog = new ContentDialog
        {
            Title = "错误",
            Content = message,
            CloseButtonText = "确定",
            XamlRoot = XamlRoot
        };
        await errorDialog.ShowAsync();
    }
}
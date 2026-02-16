using LoliaFrpClient.Constants;
using LoliaFrpClient.Controls;
using LoliaFrpClient.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace LoliaFrpClient.Pages
{
    /// <summary>
    /// Settings page with theme toggle and login functionality
    /// </summary>
    public sealed partial class Settings : Page
    {
        private readonly SettingsStorage _settings = SettingsStorage.Instance;
        private readonly ApiClientProvider _apiClientProvider = ApiClientProvider.Instance;

        public Settings()
        {
            this.InitializeComponent();
            DataContext = this;
            UpdateLoginStatus();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // 使用 UriBuilder 构建 OAuth 授权 URL
            var callbackUrl = OAuthCallbackService.GetCallbackUrl();
            var uriBuilder = new UriBuilder(OAuthConstants.AuthorizeEndpoint);
            
            // 添加查询参数
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            query["client_id"] = OAuthConstants.ClientId;
            query["redirect_uri"] = callbackUrl;
            query["response_type"] = OAuthConstants.ResponseType;
            query["scope"] = OAuthConstants.Scope;
            uriBuilder.Query = query.ToString();

            // 创建并显示登录弹窗
            var loginDialog = new LoginDialog(uriBuilder.ToString(), OnCallbackReceived);
            loginDialog.XamlRoot = this.XamlRoot;
            await loginDialog.ShowAsync();
        }

        /// <summary>
        /// 接收到 OAuth 回调时的处理
        /// </summary>
        private async void OnCallbackReceived(OAuthCallbackService.OAuthCallbackResult result)
        {
            if (result.Error != null)
            {
                // 显示错误信息
                _ = ShowDialogAsync($"授权失败: {result.Error}{(string.IsNullOrEmpty(result.ErrorDescription) ? "" : $" - {result.ErrorDescription}")}");
                return;
            }

            if (result.Code != null)
            {
                try
                {
                    // 使用 code 交换 access token
                    var token = await OAuthTokenService.ExchangeCodeForTokenAsync(result.Code);

                    // 存储 OAuthToken 为 "Bearer <token>"
                    _settings.OAuthToken = token;

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
        }

        /// <summary>
        /// 更新登录状态显示
        /// </summary>
        private void UpdateLoginStatus()
        {
            var isLoggedIn = !string.IsNullOrEmpty(_settings.OAuthToken);
            
            // 更新登录和退出登录按钮状态
            LoginButton.Visibility = isLoggedIn ? Visibility.Collapsed : Visibility.Visible;
            LogoutButton.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;

            // 更新登录状态显示
            var statusPanel = (StackPanel)RootGrid.Children[1];
            statusPanel.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
            LoginStatusText.Text = isLoggedIn ? "已登录" : "未登录";
        }

        /// <summary>
        /// 退出登录按钮点击事件
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
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}

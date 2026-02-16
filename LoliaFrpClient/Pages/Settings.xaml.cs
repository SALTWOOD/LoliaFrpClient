using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LoliaFrpClient.Models;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.Pages
{
    /// <summary>
    /// Settings page with theme toggle and login functionality
    /// </summary>
    public sealed partial class Settings : Page
    {
        private readonly SettingsStorage _settings = SettingsStorage.Instance;

        public Settings()
        {
            this.InitializeComponent();
            DataContext = this;
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement login logic
            // This method should return a LoginResult with token and user info
            // Do not block UI thread, use async/await
            // After login completes, store the token using:
            // ApiConfigurationService.Instance.SetAuthorization(loginResult.Token);
            
            // For now, just show a placeholder message
            await ShowDialogAsync("登录功能待实现");
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

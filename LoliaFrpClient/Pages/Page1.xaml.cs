using LoliaFrpClient.Models;
using LoliaFrpClient.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;

namespace LoliaFrpClient.Pages
{
    /// <summary>
    /// 用户信息页面
    /// </summary>
    public sealed partial class Page1 : Page, INotifyPropertyChanged
    {
        private readonly ApiClientProvider _apiClientProvider;
        public UserInfoViewModel ViewModel { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Page1()
        {
            this.InitializeComponent();
            _apiClientProvider = ApiClientProvider.Instance;
            ViewModel = new UserInfoViewModel();
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            await LoadUserInfoAsync();
        }

        private async System.Threading.Tasks.Task LoadUserInfoAsync()
        {
            try
            {
                var response = await _apiClientProvider.Client.User.Info.GetAsInfoGetResponseAsync();
                var data = response?.Data;
                if (data != null)
                {
                    ViewModel.Id = data.Id ?? 0;
                    ViewModel.Username = data.Username ?? string.Empty;
                    ViewModel.Email = data.Email ?? string.Empty;
                    ViewModel.Avatar = data.Avatar ?? string.Empty;
                    ViewModel.Role = data.Role ?? string.Empty;
                    ViewModel.KycStatus = data.KycStatus ?? string.Empty;
                    ViewModel.CreatedAt = data.CreatedAt ?? string.Empty;
                    ViewModel.MaxTunnelCount = data.MaxTunnelCount ?? 0;
                    ViewModel.TrafficLimit = data.TrafficLimit ?? 0;
                    ViewModel.TrafficUsed = data.TrafficUsed ?? 0;
                    ViewModel.BandwidthLimit = data.BandwidthLimit ?? 0;
                    ViewModel.HasKyc = data.HasKyc ?? false;
                    ViewModel.IsBaned = data.IsBaned ?? false;
                    ViewModel.TodayChecked = data.TodayChecked ?? false;

                    OnPropertyChanged(nameof(IsBanedText));
                    OnPropertyChanged(nameof(BanedColor));
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("加载用户信息失败", ex.Message);
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await LoadUserInfoAsync();
        }

        public string IsBanedText => ViewModel.IsBaned ? "已封禁" : "正常";

        public Brush BanedColor
        {
            get
            {
                var brushName = ViewModel.IsBaned ? "SystemFillColorCautionBrush" : "SystemFillColorSuccessBrush";
                return Application.Current.Resources[brushName] as Brush ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
            }
        }

        private async System.Threading.Tasks.Task ShowErrorDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}

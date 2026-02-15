using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using LoliaFrpClient.Models;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.Pages
{
    /// <summary>
    /// 用户信息页面
    /// </summary>
    public sealed partial class Page1 : Page, INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        public UserInfoViewModel ViewModel { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Page1()
        {
            this.InitializeComponent();
            _apiService = ApiService.Instance;
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
                var userInfo = await _apiService.GetUserInfoAsync();
                if (userInfo != null)
                {
                    ViewModel.Id = userInfo.Id;
                    ViewModel.Username = userInfo.Username;
                    ViewModel.Email = userInfo.Email;
                    ViewModel.Avatar = userInfo.Avatar;
                    ViewModel.Role = userInfo.Role;
                    ViewModel.KycStatus = userInfo.KycStatus;
                    ViewModel.CreatedAt = userInfo.CreatedAt;
                    ViewModel.MaxTunnelCount = userInfo.MaxTunnelCount;
                    ViewModel.TrafficLimit = userInfo.TrafficLimit;
                    ViewModel.TrafficUsed = userInfo.TrafficUsed;
                    ViewModel.BandwidthLimit = userInfo.BandwidthLimit;
                    ViewModel.HasKyc = userInfo.HasKyc;
                    ViewModel.IsBaned = userInfo.IsBaned;
                    ViewModel.TodayChecked = userInfo.TodayChecked;

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

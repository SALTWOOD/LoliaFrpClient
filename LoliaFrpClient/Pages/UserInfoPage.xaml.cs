using LoliaFrpClient.Models;
using LoliaFrpClient.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace LoliaFrpClient.Pages
{
    /// <summary>
    /// 用户信息页面
    /// </summary>
    public sealed partial class UserInfoPage : Page, INotifyPropertyChanged
    {
        private readonly ApiClientProvider _apiClientProvider;
        public UserInfoViewModel ViewModel { get; }

        private ObservableCollection<TunnelTrafficViewModel> _tunnelTraffics = new ObservableCollection<TunnelTrafficViewModel>();
        private ObservableCollection<DailyTrafficViewModel> _dailyTraffics = new ObservableCollection<DailyTrafficViewModel>();

        public List<DailyTrafficViewModel> DailyTrafficsList => _dailyTraffics.ToList();

        public ObservableCollection<TunnelTrafficViewModel> TunnelTraffics
        {
            get => _tunnelTraffics;
            set
            {
                _tunnelTraffics = value;
                OnPropertyChanged(nameof(TunnelTraffics));
            }
        }

        public ObservableCollection<DailyTrafficViewModel> DailyTraffics
        {
            get => _dailyTraffics;
            set
            {
                _dailyTraffics = value;
                OnPropertyChanged(nameof(DailyTraffics));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public UserInfoPage()
        {
            this.InitializeComponent();
            _apiClientProvider = ApiClientProvider.Instance;
            ViewModel = new UserInfoViewModel();
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            await LoadUserInfoAsync();
            await LoadTrafficStatsAsync();
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
                    OnPropertyChanged(nameof(KycStatusColor));
                    OnPropertyChanged(nameof(KycStatusBackgroundColor));
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("加载用户信息失败", ex.Message);
            }
        }

        private async System.Threading.Tasks.Task LoadTrafficStatsAsync()
        {
            LoadingRing.IsActive = true;
            TunnelTrafficListView.Visibility = Visibility.Collapsed;

            try
            {
                // 加载每日流量统计
                var dailyResponse = await _apiClientProvider.Client.User.Traffic.Daily.GetAsDailyGetResponseAsync(
                    config => config.QueryParameters.Days = "7");

                var dailyStatsList = dailyResponse?.Data?.DailyStats;
                
                if (dailyStatsList != null)
                {
                    DailyTraffics.Clear();
                    foreach (var item in dailyStatsList)
                    {
                        DailyTraffics.Add(new DailyTrafficViewModel
                        {
                            Date = item.Date ?? string.Empty,
                            InboundBytes = item.TotalIn ?? 0,
                            OutboundBytes = item.TotalOut ?? 0
                        });
                    }
                    OnPropertyChanged(nameof(DailyTrafficsList));
                }

                // 加载隧道流量统计
                var tunnelsResponse = await _apiClientProvider.Client.User.Traffic.Tunnels.GetAsTunnelsGetResponseAsync();
                var tunnelTraffics = tunnelsResponse?.Data?.Tunnels;
                if (tunnelTraffics != null)
                {
                    TunnelTraffics.Clear();
                    foreach (var traffic in tunnelTraffics)
                    {
                        TunnelTraffics.Add(new TunnelTrafficViewModel
                        {
                            TunnelName = traffic.TunnelName ?? string.Empty,
                            InboundBytes = traffic.TotalIn ?? 0,
                            OutboundBytes = traffic.TotalOut ?? 0
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API ERROR] {ex.Message}");
                await ShowErrorDialogAsync("加载流量统计失败", ex.Message);
            }
            finally
            {
                LoadingRing.IsActive = false;
                TunnelTrafficListView.Visibility = Visibility.Visible;
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await LoadUserInfoAsync();
            await LoadTrafficStatsAsync();
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

        public Brush KycStatusColor
        {
            get
            {
                var brushName = ViewModel.KycStatus switch
                {
                    "init" => "SystemFillColorCautionBrush",
                    "certifying" => "SystemFillColorAttentionBrush",
                    "success" => "SystemFillColorSuccessBrush",
                    "failed" => "SystemFillColorCriticalBrush",
                    _ => "SystemFillColorNeutralBrush"
                };
                return Application.Current.Resources[brushName] as Brush ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
            }
        }

        public Brush KycStatusBackgroundColor
        {
            get
            {
                var brushName = ViewModel.KycStatus switch
                {
                    "init" => "SystemFillColorCautionBackgroundBrush",
                    "certifying" => "SystemFillColorAttentionBackgroundBrush",
                    "success" => "SystemFillColorSuccessBackgroundBrush",
                    "failed" => "SystemFillColorCriticalBackgroundBrush",
                    _ => "SystemFillColorNeutralBackgroundBrush"
                };
                return Application.Current.Resources[brushName] as Brush ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
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

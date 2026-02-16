using LoliaFrpClient.Models;
using LoliaFrpClient.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace LoliaFrpClient.Pages
{
    /// <summary>
    /// 流量统计页面
    /// </summary>
    public sealed partial class Page3 : Page, INotifyPropertyChanged
    {
        private readonly ApiClientProvider _apiClientProvider;
        private TrafficStatsViewModel _trafficStats = new TrafficStatsViewModel();
        private ObservableCollection<TunnelTrafficViewModel> _tunnelTraffics = new ObservableCollection<TunnelTrafficViewModel>();

        public TrafficStatsViewModel TrafficStats
        {
            get => _trafficStats;
            set
            {
                _trafficStats = value;
                OnPropertyChanged(nameof(TrafficStats));
            }
        }

        public ObservableCollection<TunnelTrafficViewModel> TunnelTraffics
        {
            get => _tunnelTraffics;
            set
            {
                _tunnelTraffics = value;
                OnPropertyChanged(nameof(TunnelTraffics));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Page3()
        {
            this.InitializeComponent();
            _apiClientProvider = ApiClientProvider.Instance;
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            await LoadTrafficStatsAsync();
        }

        private async System.Threading.Tasks.Task LoadTrafficStatsAsync()
        {
            LoadingRing.IsActive = true;
            TunnelTrafficListView.Visibility = Visibility.Collapsed;

            try
            {
                // 加载总体流量统计
                var statsResponse = await _apiClientProvider.Client.User.Traffic.Stats.GetAsStatsGetResponseAsync();
                var trafficStats = statsResponse?.Data;
                if (trafficStats != null)
                {
                    TrafficStats = new TrafficStatsViewModel
                    {
                        UserId = trafficStats.UserId ?? string.Empty,
                        Username = trafficStats.Username ?? string.Empty,
                        TrafficLimit = (int)(trafficStats.TrafficLimit ?? 0),
                        TrafficUsed = (int)(trafficStats.TrafficUsed ?? 0),
                        TrafficRemaining = (int)(trafficStats.TrafficRemaining ?? 0)
                    };
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
            await LoadTrafficStatsAsync();
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

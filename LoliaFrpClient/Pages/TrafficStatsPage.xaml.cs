using LoliaFrpClient.Models;
using LoliaFrpClient.Services;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;

namespace LoliaFrpClient.Pages
{
    /// <summary>
    /// 流量统计页面
    /// </summary>
    public sealed partial class TrafficStatsPage : Page, INotifyPropertyChanged
    {
        private readonly ApiClientProvider _apiClientProvider;
        private TrafficStatsViewModel _trafficStats = new TrafficStatsViewModel();
        private ObservableCollection<TunnelTrafficViewModel> _tunnelTraffics = new ObservableCollection<TunnelTrafficViewModel>();
        private ObservableCollection<DailyTrafficViewModel> _dailyTraffics = new ObservableCollection<DailyTrafficViewModel>();

        public List<DailyTrafficViewModel> DailyTrafficsList => _dailyTraffics.ToList();

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

        public TrafficStatsPage()
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
                var statsResponse = await _apiClientProvider.Client.User.Traffic.Stats.GetAsStatsGetResponseAsync();
                var trafficStats = statsResponse?.Data;
                if (trafficStats != null)
                {
                    TrafficStats = new TrafficStatsViewModel
                    {
                        UserId = trafficStats.UserId ?? string.Empty,
                        Username = trafficStats.Username ?? string.Empty,
                        TrafficLimit = (trafficStats.TrafficLimit ?? 0),
                        TrafficUsed = (trafficStats.TrafficUsed ?? 0),
                        TrafficRemaining = (trafficStats.TrafficRemaining ?? 0)
                    };
                }

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

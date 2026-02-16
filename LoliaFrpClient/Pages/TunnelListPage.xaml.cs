using LoliaFrpClient.Models;
using LoliaFrpClient.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace LoliaFrpClient.Pages
{
    /// <summary>
    /// 隧道列表页面
    /// </summary>
    public sealed partial class TunnelListPage : Page, INotifyPropertyChanged
    {
        private readonly ApiClientProvider _apiClientProvider;
        private readonly FrpcManager _frpcManager = ServiceLocator.FrpcManager;
        private ObservableCollection<TunnelViewModel> _tunnels = new ObservableCollection<TunnelViewModel>();
        private ObservableCollection<TunnelViewModel> _filteredTunnels = new ObservableCollection<TunnelViewModel>();
        private string _searchText = string.Empty;
        private string _filterType = "all";
        private readonly Dictionary<int, Services.FrpcProcessInfo> _tunnelProcesses = new Dictionary<int, Services.FrpcProcessInfo>();

        public ObservableCollection<TunnelViewModel> Tunnels
        {
            get => _tunnels;
            set
            {
                _tunnels = value;
                OnPropertyChanged(nameof(Tunnels));
                UpdateFilteredTunnels();
            }
        }

        public ObservableCollection<TunnelViewModel> FilteredTunnels => _filteredTunnels;

        /// <summary>
        /// 隧道总数
        /// </summary>
        public int TotalTunnels => Tunnels.Count;

        /// <summary>
        /// 运行中的隧道数量
        /// </summary>
        public int ActiveTunnels => Tunnels.Count(t => t.Status == "active");

        /// <summary>
        /// 未激活的隧道数量
        /// </summary>
        public int InactiveTunnels => Tunnels.Count(t => t.Status == "inactive");

        /// <summary>
        /// 已禁用的隧道数量
        /// </summary>
        public int DisabledTunnels => Tunnels.Count(t => t.Status == "disabled");

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public TunnelListPage()
        {
            this.InitializeComponent();
            _apiClientProvider = ApiClientProvider.Instance;
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            await LoadTunnelsAsync();
        }

        private async System.Threading.Tasks.Task LoadTunnelsAsync()
        {
            LoadingRing.IsActive = true;
            TunnelListView.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Collapsed;

            try
            {
                var response = await _apiClientProvider.Client.User.Tunnel.GetAsTunnelGetResponseAsync();
                var tunnelList = response?.Data?.List;
                if (tunnelList != null)
                {
                    Tunnels.Clear();
                    foreach (var tunnel in tunnelList)
                    {
                        var tunnelId = tunnel.Id ?? 0;
                        var viewModel = new TunnelViewModel
                        {
                            Id = tunnelId,
                            Name = tunnel.Name ?? string.Empty,
                            Type = tunnel.Type ?? string.Empty,
                            Status = tunnel.Status ?? string.Empty,
                            Remark = tunnel.Remark ?? string.Empty,
                            CustomDomain = tunnel.CustomDomain ?? string.Empty,
                            LocalIp = tunnel.LocalIp ?? string.Empty,
                            LocalPort = tunnel.LocalPort ?? 0,
                            RemotePort = tunnel.RemotePort ?? 0,
                            NodeId = tunnel.NodeId ?? 0,
                            BandwidthLimit = tunnel.BandwidthLimit ?? 0
                        };
                        // 根据 FrpcManager 中的实际进程状态设置 IsEnabled，避免刷新页面时错误触发 Toggled 事件
                        viewModel.IsEnabled = _frpcManager.IsTunnelProcessRunning(tunnelId);
                        Tunnels.Add(viewModel);
                    }
                    UpdateFilteredTunnels();
                    UpdateStatistics();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("加载隧道列表失败", ex.Message);
            }
            finally
            {
                LoadingRing.IsActive = false;
                
                // 显示空状态或列表
                if (FilteredTunnels.Count == 0)
                {
                    EmptyStatePanel.Visibility = Visibility.Visible;
                    TunnelListView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    EmptyStatePanel.Visibility = Visibility.Collapsed;
                    TunnelListView.Visibility = Visibility.Visible;
                }
            }
        }

        /// <summary>
        /// 更新筛选后的隧道列表
        /// </summary>
        private void UpdateFilteredTunnels()
        {
            FilteredTunnels.Clear();

            var query = Tunnels.AsEnumerable();

            // 按类型筛选
            if (_filterType != "all")
            {
                query = query.Where(t => t.Type == _filterType);
            }

            // 按搜索文本筛选
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                query = query.Where(t => 
                    t.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    t.Remark.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    t.CustomDomain.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                );
            }

            foreach (var tunnel in query)
            {
                FilteredTunnels.Add(tunnel);
            }
        }

        /// <summary>
        /// 更新统计数据
        /// </summary>
        private void UpdateStatistics()
        {
            OnPropertyChanged(nameof(TotalTunnels));
            OnPropertyChanged(nameof(ActiveTunnels));
            OnPropertyChanged(nameof(InactiveTunnels));
            OnPropertyChanged(nameof(DisabledTunnels));
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await LoadTunnelsAsync();
        }

        /// <summary>
        /// 搜索文本变化
        /// </summary>
        private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                _searchText = sender.Text;
                UpdateFilteredTunnels();
                UpdateEmptyState();
            }
        }

        /// <summary>
        /// 搜索查询提交
        /// </summary>
        private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            _searchText = args.QueryText;
            UpdateFilteredTunnels();
            UpdateEmptyState();
        }

        /// <summary>
        /// 筛选类型变化
        /// </summary>
        private void OnFilterTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _filterType = selectedItem.Tag?.ToString() ?? "all";
                UpdateFilteredTunnels();
                UpdateEmptyState();
            }
        }

        /// <summary>
        /// 更新空状态显示
        /// </summary>
        private void UpdateEmptyState()
        {
            if (FilteredTunnels.Count == 0)
            {
                EmptyStatePanel.Visibility = Visibility.Visible;
                TunnelListView.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyStatePanel.Visibility = Visibility.Collapsed;
                TunnelListView.Visibility = Visibility.Visible;
            }
        }

        private async void OnTunnelItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TunnelViewModel tunnel)
            {
                await ShowTunnelDetailDialogAsync(tunnel);
            }
        }

        private async System.Threading.Tasks.Task ShowTunnelDetailDialogAsync(TunnelViewModel tunnel)
        {
            var dialog = new ContentDialog
            {
                Title = "隧道详情",
                Content = CreateTunnelDetailContent(tunnel),
                CloseButtonText = "关闭",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private UIElement CreateTunnelDetailContent(TunnelViewModel tunnel)
        {
            var stackPanel = new StackPanel { Spacing = 12 };

            var infoGrid = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int row = 0;
            AddInfoRow(infoGrid, row++, "名称:", tunnel.Name);
            AddInfoRow(infoGrid, row++, "类型:", tunnel.TypeDisplayText);
            AddInfoRow(infoGrid, row++, "状态:", tunnel.StatusDisplayText);
            AddInfoRow(infoGrid, row++, "备注:", tunnel.Remark);
            AddInfoRow(infoGrid, row++, "自定义域名:", tunnel.CustomDomain);
            AddInfoRow(infoGrid, row++, "本地地址:", $"{tunnel.LocalIp}:{tunnel.LocalPort}");
            AddInfoRow(infoGrid, row++, "远程端口:", tunnel.RemotePort.ToString());
            AddInfoRow(infoGrid, row++, "节点 ID:", tunnel.NodeId.ToString());

            stackPanel.Children.Add(infoGrid);
            return stackPanel;
        }

        private void AddInfoRow(Grid grid, int row, string label, string value)
        {
            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);

            var valueBlock = new TextBlock
            {
                Text = value,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);

            grid.Children.Add(labelBlock);
            grid.Children.Add(valueBlock);
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

        /// <summary>
        /// 切换隧道启用状态
        /// </summary>
        private async void OnTunnelSwitchToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch && toggleSwitch.Tag is TunnelViewModel tunnel)
            {
                // 保存原始状态，以便在操作失败时恢复
                bool originalState = tunnel.IsEnabled;
                bool newState = toggleSwitch.IsOn;
                
                if (newState)
                {
                    // 启用隧道
                    bool success = await EnableTunnelAsync(tunnel);
                    if (!success)
                    {
                        // 操作失败，恢复原始状态
                        tunnel.IsEnabled = originalState;
                    }
                    else
                    {
                        // 操作成功，刷新数据以更新统计信息
                        await LoadTunnelsAsync();
                    }
                }
                else
                {
                    // 禁用隧道
                    bool success = await DisableTunnelAsync(tunnel);
                    if (!success)
                    {
                        // 操作失败，恢复原始状态
                        tunnel.IsEnabled = originalState;
                    }
                    else
                    {
                        // 操作成功，刷新数据以更新统计信息
                        await LoadTunnelsAsync();
                    }
                }
            }
        }

        /// <summary>
        /// 启用隧道
        /// </summary>
        private async System.Threading.Tasks.Task<bool> EnableTunnelAsync(TunnelViewModel tunnel)
        {
            try
            {
                // 检查 frpc 是否已安装
                if (!_frpcManager.IsFrpcReady())
                {
                    await ShowErrorDialogAsync("frpc 未安装", "请在设置页面先安装 frpc");
                    return false;
                }

                // 获取 frpc 配置 token
                var configResponse = await _apiClientProvider.Client.User.Tunnel[tunnel.Name].GetAsWithTunnel_nameGetResponseAsync();
                var token = configResponse?.Data?.TunnelToken;

                if (string.IsNullOrEmpty(token))
                {
                    await ShowErrorDialogAsync("获取配置失败", "无法获取 frpc token");
                    return false;
                }

                // 使用 frpc 启动隧道
                var tunnelId = tunnel.Id.ToString();
                var success = _frpcManager.StartTunnelProcess(tunnel.Id, $"-t {tunnelId}:{token}");

                if (success)
                {
                    tunnel.IsEnabled = true;
                    await ShowErrorDialogAsync("启用成功", $"隧道 {tunnel.Name} 已启用");
                    return true;
                }
                else
                {
                    await ShowErrorDialogAsync("启用失败", "无法启动 frpc 进程");
                    return false;
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("启用失败", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 禁用隧道
        /// </summary>
        private async System.Threading.Tasks.Task<bool> DisableTunnelAsync(TunnelViewModel tunnel)
        {
            try
            {
                // 停止指定隧道的 frpc 进程
                var success = _frpcManager.StopTunnelProcess(tunnel.Id);

                if (success)
                {
                    tunnel.IsEnabled = false;
                    await ShowErrorDialogAsync("禁用成功", $"隧道 {tunnel.Name} 已禁用");
                    return true;
                }
                else
                {
                    await ShowErrorDialogAsync("禁用失败", "无法停止 frpc 进程");
                    return false;
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("禁用失败", ex.Message);
                return false;
            }
        }
    }
}

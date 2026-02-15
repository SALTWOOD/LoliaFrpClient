using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using LoliaFrpClient.Models;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.Pages
{
    /// <summary>
    /// 隧道列表页面
    /// </summary>
    public sealed partial class Page2 : Page, INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private ObservableCollection<TunnelViewModel> _tunnels = new ObservableCollection<TunnelViewModel>();

        public ObservableCollection<TunnelViewModel> Tunnels
        {
            get => _tunnels;
            set
            {
                _tunnels = value;
                OnPropertyChanged(nameof(Tunnels));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Page2()
        {
            this.InitializeComponent();
            _apiService = ApiService.Instance;
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

            try
            {
                var tunnelList = await _apiService.GetTunnelListAsync();
                Tunnels.Clear();
                foreach (var tunnel in tunnelList)
                {
                    Tunnels.Add(tunnel);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("加载隧道列表失败", ex.Message);
            }
            finally
            {
                LoadingRing.IsActive = false;
                TunnelListView.Visibility = Visibility.Visible;
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await LoadTunnelsAsync();
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
    }
}

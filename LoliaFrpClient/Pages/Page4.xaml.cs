using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.Pages
{
    /// <summary>
    /// 节点列表页面
    /// </summary>
    public sealed partial class Page4 : Page, INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private ObservableCollection<NodeInfo> _nodes = new ObservableCollection<NodeInfo>();

        public ObservableCollection<NodeInfo> Nodes
        {
            get => _nodes;
            set
            {
                _nodes = value;
                OnPropertyChanged(nameof(Nodes));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Page4()
        {
            this.InitializeComponent();
            _apiService = ApiService.Instance;
            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            await LoadNodesAsync();
        }

        private async System.Threading.Tasks.Task LoadNodesAsync()
        {
            LoadingRing.IsActive = true;
            NodesListView.Visibility = Visibility.Collapsed;

            try
            {
                var nodeList = await _apiService.GetNodesAsync();
                Nodes.Clear();
                foreach (var node in nodeList)
                {
                    Nodes.Add(node);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("加载节点列表失败", ex.Message);
            }
            finally
            {
                LoadingRing.IsActive = false;
                NodesListView.Visibility = Visibility.Visible;
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await LoadNodesAsync();
        }

        private async void OnNodeItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is NodeInfo node)
            {
                await ShowNodeDetailDialogAsync(node);
            }
        }

        private async System.Threading.Tasks.Task ShowNodeDetailDialogAsync(NodeInfo node)
        {
            var dialog = new ContentDialog
            {
                Title = "节点详情",
                Content = CreateNodeDetailContent(node),
                CloseButtonText = "关闭",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private UIElement CreateNodeDetailContent(NodeInfo node)
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

            int row = 0;
            AddInfoRow(infoGrid, row++, "名称:", node.Name);
            AddInfoRow(infoGrid, row++, "位置:", node.Location);
            AddInfoRow(infoGrid, row++, "地址:", $"{node.Host}:{node.Port}");
            AddInfoRow(infoGrid, row++, "状态:", node.StatusText);
            AddInfoRow(infoGrid, row++, "带宽:", $"{node.Bandwidth} Mbps");
            AddInfoRow(infoGrid, row++, "在线:", node.Online.ToString());

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

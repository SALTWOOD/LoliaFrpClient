using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using LoliaFrpClient.Controls;
using LoliaFrpClient.Models;
using LoliaFrpClient.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace LoliaFrpClient.Pages;

/// <summary>
///     隧道列表页面
/// </summary>
public sealed partial class TunnelListPage : Page, INotifyPropertyChanged
{
    private readonly TunnelService _tunnelService = new();
    private string _filterType = "all";
    private string _searchText = string.Empty;
    private ObservableCollection<TunnelViewModel> _tunnels = new();

    public TunnelListPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

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

    public ObservableCollection<TunnelViewModel> FilteredTunnels { get; } = new();

    public int TotalTunnels => Tunnels.Count;
    public int ActiveTunnels => Tunnels.Count(t => t.Status == "active");
    public int InactiveTunnels => Tunnels.Count(t => t.Status == "inactive");
    public int DisabledTunnels => Tunnels.Count(t => t.Status == "disabled");

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await LoadTunnelsAsync();
    }

    private async Task LoadTunnelsAsync()
    {
        SetLoadingState(true);

        try
        {
            ReplaceTunnels(await _tunnelService.GetTunnelsAsync());
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("加载隧道列表失败", ex.Message);
        }
        finally
        {
            SetLoadingState(false);
            UpdateListState();
        }
    }

    private void UpdateFilteredTunnels()
    {
        FilteredTunnels.Clear();

        foreach (var tunnel in _tunnelService.FilterTunnels(Tunnels, _filterType, _searchText))
        {
            FilteredTunnels.Add(tunnel);
        }

        UpdateListState();
    }

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

    private async void OnCreateTunnelClick(object sender, RoutedEventArgs e)
    {
        await CreateTunnelAsync();
    }

    private async Task CreateTunnelAsync()
    {
        var dialog = new CreateTunnelDialog();
        var result = await DialogManager.Instance.ShowDialogAsync(dialog);

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _tunnelService.CreateTunnelAsync(dialog.GetTunnelRequestBody());
            await ShowErrorDialogAsync("创建成功", "隧道已成功创建");
            await LoadTunnelsAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("创建失败", ex.Message);
        }
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        _searchText = sender.Text;
        UpdateFilteredTunnels();
    }

    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _searchText = args.QueryText;
        UpdateFilteredTunnels();
    }

    private void OnFilterTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem selectedItem)
        {
            return;
        }

        _filterType = selectedItem.Tag?.ToString() ?? "all";
        UpdateFilteredTunnels();
    }

    private void UpdateListState()
    {
        if (LoadingRing.IsActive)
        {
            return;
        }

        var hasItems = FilteredTunnels.Count > 0;
        EmptyStatePanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        TunnelListView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnTunnelCardRightClick(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TunnelViewModel tunnel)
        {
            return;
        }

        e.Handled = true;
        await ShowTunnelDetailDialogAsync(tunnel);
    }

    private async Task ShowTunnelDetailDialogAsync(TunnelViewModel tunnel)
    {
        var dialog = new ContentDialog
        {
            Title = "隧道详情",
            Content = CreateTunnelDetailContent(tunnel),
            CloseButtonText = "关闭",
            PrimaryButtonText = "编辑",
            SecondaryButtonText = "删除"
        };

        var result = await DialogManager.Instance.ShowDialogAsync(dialog);

        if (result == ContentDialogResult.Primary)
        {
            await ShowErrorDialogAsync("功能暂不可用", "编辑隧道功能暂未实现，请等待API支持");
            return;
        }

        if (result == ContentDialogResult.Secondary)
        {
            await DeleteTunnelAsync(tunnel);
        }
    }

    private async Task DeleteTunnelAsync(TunnelViewModel tunnel)
    {
        var result = await DialogManager.Instance.ShowConfirmAsync(
            "确认删除",
            $"确定要删除隧道 \"{tunnel.Name}\" 吗？此操作不可撤销。",
            "删除",
            "取消");

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _tunnelService.DeleteTunnelAsync(tunnel);
            await ShowErrorDialogAsync("删除成功", $"隧道 \"{tunnel.Name}\" 已成功删除");
            await LoadTunnelsAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("删除失败", ex.Message);
        }
    }

    private UIElement CreateTunnelDetailContent(TunnelViewModel tunnel)
    {
        var stackPanel = new StackPanel { Spacing = 12 };

        var infoGrid = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var i = 0; i < 8; i++)
        {
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var row = 0;
        AddInfoRow(infoGrid, row++, "名称:", tunnel.Name);
        AddInfoRow(infoGrid, row++, "类型:", tunnel.TypeDisplayText);
        AddInfoRow(infoGrid, row++, "状态:", tunnel.StatusDisplayText);
        AddInfoRow(infoGrid, row++, "备注:", tunnel.Remark);
        AddInfoRow(infoGrid, row++, "自定义域名:", tunnel.CustomDomain);
        AddInfoRow(infoGrid, row++, "本地地址:", $"{tunnel.LocalIp}:{tunnel.LocalPort}");
        AddInfoRow(infoGrid, row++, "远程端口:", tunnel.RemotePort.ToString());
        AddInfoRow(infoGrid, row, "节点 ID:", tunnel.NodeId.ToString());

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

    private async Task ShowErrorDialogAsync(string title, string message)
    {
        await DialogManager.Instance.ShowErrorAsync(title, message);
    }

    private async void OnTunnelCardDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not TunnelViewModel tunnel)
        {
            return;
        }

        var originalState = tunnel.IsEnabled;

        if (!originalState)
        {
            var success = await EnableTunnelAsync(tunnel);
            if (!success)
            {
                tunnel.IsEnabled = originalState;
                return;
            }

            await DialogManager.Instance.ShowMessageAsync("启用成功", $"隧道 \"{tunnel.Name}\" 已成功启动");
            await LoadTunnelsAsync();
            return;
        }

        var disableSuccess = await DisableTunnelAsync(tunnel);
        if (!disableSuccess)
        {
            tunnel.IsEnabled = originalState;
            return;
        }

        await DialogManager.Instance.ShowMessageAsync("关闭成功", $"隧道 \"{tunnel.Name}\" 已成功关闭");
        await LoadTunnelsAsync();
    }

    private async Task<bool> EnableTunnelAsync(TunnelViewModel tunnel)
    {
        try
        {
            await _tunnelService.StartTunnelAsync(tunnel);
            return true;
        }
        catch (InvalidOperationException)
        {
            await ShowErrorDialogAsync("启用失败", "无法获取隧道连接密钥 (Token)");
            return false;
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("启用失败", ex.Message);
            return false;
        }
    }

    private async Task<bool> DisableTunnelAsync(TunnelViewModel tunnel)
    {
        try
        {
            _tunnelService.StopTunnel(tunnel);
            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("禁用失败", ex.Message);
            return false;
        }
    }

    private void ReplaceTunnels(System.Collections.Generic.IEnumerable<TunnelViewModel> tunnels)
    {
        Tunnels.Clear();

        foreach (var tunnel in tunnels)
        {
            Tunnels.Add(tunnel);
        }

        UpdateFilteredTunnels();
        UpdateStatistics();
    }

    private void SetLoadingState(bool isLoading)
    {
        LoadingRing.IsActive = isLoading;

        if (isLoading)
        {
            TunnelListView.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
        }
    }
}

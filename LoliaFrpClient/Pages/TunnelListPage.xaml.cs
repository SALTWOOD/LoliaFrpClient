using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using LoliaFrpClient.Constants;
using LoliaFrpClient.Controls;
using LoliaFrpClient.Models;
using LoliaFrpClient.Services;
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
    private string _filterType = TunnelType.All;
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
    public int ActiveTunnels => Tunnels.Count(t => t.Status == TunnelStatus.Active);
    public int InactiveTunnels => Tunnels.Count(t => t.Status == TunnelStatus.Inactive);
    public int DisabledTunnels => Tunnels.Count(t => t.Status == TunnelStatus.Disabled);

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
        await PageLoader.RunAsync(SetLoadingState,
            async () => ReplaceTunnels(await _tunnelService.GetTunnelsAsync()),
            "加载隧道列表失败");

        UpdateListState();
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
            if (AuthErrorHelper.ShouldSilence(ex))
            {
                return;
            }

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

        _filterType = selectedItem.Tag?.ToString() ?? TunnelType.All;
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
            Content = TunnelDetailContent.Create(tunnel),
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
            if (AuthErrorHelper.ShouldSilence(ex))
            {
                return;
            }

            await ShowErrorDialogAsync("删除失败", ex.Message);
        }
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
        var result = await _tunnelService.TryEnableAsync(tunnel);

        switch (result.Kind)
        {
            case TunnelActionResultKind.Success:
                return true;

            case TunnelActionResultKind.FrpcNotInstalled:
                var confirm = await DialogManager.Instance.ShowConfirmAsync(
                    "未安装 frpc",
                    "尚未安装 frpc 客户端，无法启动隧道。是否前往设置页面安装？",
                    "前往设置");

                if (confirm == ContentDialogResult.Primary)
                {
                    MainWindow.NavigateTo<Settings>();
                }

                return false;

            case TunnelActionResultKind.TokenUnavailable:
                await ShowErrorDialogAsync("启用失败", "无法获取隧道连接密钥 (Token)");
                return false;

            case TunnelActionResultKind.Silent:
                return false;

            default:
                await ShowErrorDialogAsync("启用失败", result.ErrorMessage ?? "未知错误");
                return false;
        }
    }

    private async Task<bool> DisableTunnelAsync(TunnelViewModel tunnel)
    {
        var result = _tunnelService.TryDisable(tunnel);

        if (result.IsSuccess) return true;

        await ShowErrorDialogAsync("禁用失败", result.ErrorMessage ?? "未知错误");
        return false;
    }

    private void ReplaceTunnels(System.Collections.Generic.IEnumerable<TunnelViewModel> tunnels)
    {
        Tunnels.ReplaceWith(tunnels);

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

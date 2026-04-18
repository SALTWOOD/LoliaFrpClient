using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using LoliaFrpClient.Models;
using LoliaFrpClient.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace LoliaFrpClient.Pages;

/// <summary>
///     用户信息页面
/// </summary>
public sealed partial class UserInfoPage : Page, INotifyPropertyChanged
{
    private readonly UserInfoService _userInfoService = new();
    private ObservableCollection<DailyTrafficViewModel> _dailyTraffics = new();
    private ObservableCollection<TunnelTrafficViewModel> _tunnelTraffics = new();

    public UserInfoPage()
    {
        InitializeComponent();
        ViewModel = new UserInfoViewModel();
        Loaded += OnPageLoaded;
    }

    public UserInfoViewModel ViewModel { get; private set; }

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
            OnPropertyChanged(nameof(DailyTrafficsList));
        }
    }

    public Brush BanedBrush => ResolveBrush(ViewModel.IsBanedColor, Colors.Gray);
    public Brush KycStatusBrush => ResolveBrush(ViewModel.KycStatusColor, Colors.Gray);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        SetLoadingState(true);

        try
        {
            var dashboard = await _userInfoService.GetDashboardDataAsync();
            ApplyDashboardData(dashboard);
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("加载用户数据失败", ex.Message);
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private void ApplyDashboardData(UserDashboardData dashboard)
    {
        ViewModel = dashboard.User;
        OnPropertyChanged(nameof(ViewModel));
        OnPropertyChanged(nameof(BanedBrush));
        OnPropertyChanged(nameof(KycStatusBrush));

        ReplaceCollection(DailyTraffics, dashboard.DailyTraffics);
        ReplaceCollection(TunnelTraffics, dashboard.TunnelTraffics);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static Brush ResolveBrush(string resourceKey, Color fallbackColor)
    {
        return Application.Current.Resources[resourceKey] as Brush ?? new SolidColorBrush(fallbackColor);
    }

    private void SetLoadingState(bool isLoading)
    {
        LoadingRing.IsActive = isLoading;
        TunnelTrafficListView.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await LoadDashboardAsync();
    }

    private async Task ShowErrorDialogAsync(string title, string message)
    {
        await DialogManager.Instance.ShowErrorAsync(title, message);
    }
}

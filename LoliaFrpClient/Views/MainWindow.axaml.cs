using System;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using LoliaFrpClient.Services;
using LoliaFrpClient.ViewModels;

namespace LoliaFrpClient.Views;

/// <summary>
/// 主窗口。只做导航切换,不含业务逻辑。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 首屏不经过导航事件,补一次取数。
        if (DataContext is MainViewModel viewModel) await viewModel.RefreshAsync();

        // 放在取数之后:更新是次要的,别拖慢首屏。
        if (AppSettings.Current.AutoCheckUpdates) await UpdatePrompt.CheckAndShowAsync(this);
    }

    private async void OnNavigationSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && e.SelectedItem is FANavigationViewItem item)
            await viewModel.NavigateAsync(item.Tag as string);
    }
}
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
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

    private void OnNavigationSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && e.SelectedItem is FANavigationViewItem item)
        {
            viewModel.Navigate(item.Tag as string);
        }
    }
}

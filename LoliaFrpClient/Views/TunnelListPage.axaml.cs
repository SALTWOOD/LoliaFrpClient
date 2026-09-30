using System;
using Avalonia.Controls;
using LoliaFrpClient.Services;
using LoliaFrpClient.ViewModels;

namespace LoliaFrpClient.Views;

public partial class TunnelListPage : UserControl
{
    private TunnelListViewModel? _viewModel;

    public TunnelListPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ShowDetailRequested -= OnShowDetailRequested;
            _viewModel.CreateTunnelRequested -= OnCreateTunnelRequested;
        }

        _viewModel = DataContext as TunnelListViewModel;

        if (_viewModel is not null)
        {
            _viewModel.ShowDetailRequested += OnShowDetailRequested;
            _viewModel.CreateTunnelRequested += OnCreateTunnelRequested;
        }
    }

    private async void OnShowDetailRequested(TunnelEntry entry)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner) return;

            var window = new TunnelDetailWindow(entry.Name);
            await window.ShowDialog(owner);

            if (window.Changed) await _viewModel!.ReloadAsync();
        }
        catch (Exception)
        {
            // A failed dialog must not take the page down.
        }
    }

    private async void OnCreateTunnelRequested()
    {
        try
        {
            await ShellNavigation.NavigateAsync(this, "nodes");
        }
        catch (Exception)
        {
        }
    }
}
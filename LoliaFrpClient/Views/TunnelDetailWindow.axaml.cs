using System;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using LoliaFrpClient.ViewModels;

namespace LoliaFrpClient.Views;

public partial class TunnelDetailWindow : Window
{
    private readonly TunnelDetailViewModel _viewModel;

    // Needed by the XAML runtime loader (AVLN3001) and by AOT. Anything constructed this way is
    // never shown, so the empty name and the skipped load below are harmless.
    public TunnelDetailWindow() : this(string.Empty)
    {
    }

    public TunnelDetailWindow(string tunnelName)
    {
        InitializeComponent();

        _viewModel = new TunnelDetailViewModel(tunnelName);
        DataContext = _viewModel;

        _viewModel.DeleteRequested += OnDeleteRequested;
        _viewModel.CloseRequested += Close;

        Opened += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(tunnelName)) await _viewModel.LoadAsync();
        };
    }

    // Whether the tunnel list behind this window needs reloading.
    public bool Changed => _viewModel.Changed;

    private async void OnDeleteRequested()
    {
        try
        {
            var dialog = new FAContentDialog
            {
                Title = "删除隧道",
                Content = _viewModel.DeleteHint,
                PrimaryButtonText = "删除",
                CloseButtonText = "取消"
            };

            if (await dialog.ShowAsync() == FAContentDialogResult.Primary) await _viewModel.DeleteAsync();
        }
        catch (Exception)
        {
            // A failed dialog must not take the window down.
        }
    }
}
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LoliaFrpClient.ViewModels;

namespace LoliaFrpClient.Views;

public partial class FrpcManagerPage : UserControl
{
    private readonly ScrollViewer? _logScroll;
    private FrpcManagerViewModel? _viewModel;
    private FrpcLogTab? _hookedTab;

    // Auto-follow only while parked at the bottom, otherwise scrolling up to read is fought.
    private bool _followTail = true;

    private double _lastExtentHeight;

    public FrpcManagerPage()
    {
        InitializeComponent();

        _logScroll = this.FindControl<ScrollViewer>("LogScroll");
        if (_logScroll is not null) _logScroll.ScrollChanged += OnLogScrollChanged;

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.InstallFrpcRequested -= OnInstallFrpcRequested;
            _viewModel.FrpcMissingRequested -= OnFrpcMissingRequested;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        UnhookLogLines();

        _viewModel = DataContext as FrpcManagerViewModel;

        if (_viewModel is not null)
        {
            _viewModel.InstallFrpcRequested += OnInstallFrpcRequested;
            _viewModel.FrpcMissingRequested += OnFrpcMissingRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        HookLogLines();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FrpcManagerViewModel.SelectedLogTab)) HookLogLines();
    }

    // The visible log list changes with the selected tab, so the subscription has to follow it.
    private void HookLogLines()
    {
        UnhookLogLines();

        if (_viewModel?.SelectedLogTab is { } tab)
        {
            _hookedTab = tab;
            tab.Lines.CollectionChanged += OnLogLinesChanged;
        }

        _followTail = true;
        ScrollToEnd();
    }

    private void UnhookLogLines()
    {
        if (_hookedTab is not null)
        {
            _hookedTab.Lines.CollectionChanged -= OnLogLinesChanged;
            _hookedTab = null;
        }
    }

    // Growing content is not user scrolling. Without this distinction every new line would look
    // like the user had scrolled away and auto-follow would switch itself off.
    private void OnLogScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_logScroll is null) return;

        var extentGrew = _logScroll.Extent.Height > _lastExtentHeight;
        _lastExtentHeight = _logScroll.Extent.Height;

        if (extentGrew) return;

        var distanceFromBottom = _logScroll.Extent.Height - (_logScroll.Offset.Y + _logScroll.Viewport.Height);
        _followTail = distanceFromBottom <= 8;
    }

    private void OnLogLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_followTail) return;

        ScrollToEnd();
    }

    // Posted at Background priority so layout has run; otherwise Extent is still stale and the
    // scroll lands short of the real end.
    private void ScrollToEnd()
    {
        Dispatcher.UIThread.Post(() => _logScroll?.ScrollToEnd(), DispatcherPriority.Background);
    }

    private async void OnInstallFrpcRequested()
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner) return;

            await RunInstallAsync(owner);
        }
        catch (Exception)
        {
            // A failed dialog must not take the page down.
        }
    }

    // A dialog rather than a status line: the user's intent was explicit (they pressed start)
    // and a message tucked into the corner of the page is easy to miss.
    private async void OnFrpcMissingRequested()
    {
        try
        {
            if (_viewModel is null || TopLevel.GetTopLevel(this) is not Window owner) return;

            var dialog = new FAContentDialog
            {
                Title = "尚未安装 frpc",
                Content = "启动隧道需要 frpc 核心程序。现在下载并安装吗?",
                PrimaryButtonText = "去安装",
                CloseButtonText = "取消"
            };

            if (await dialog.ShowAsync() != FAContentDialogResult.Primary)
            {
                _viewModel.CancelPendingStart();
                return;
            }

            await RunInstallAsync(owner);

            // Resume the start the user originally asked for, so they need not click again.
            await _viewModel.ResumePendingStartAsync();
        }
        catch (Exception)
        {
            _viewModel?.CancelPendingStart();
        }
    }

    private async Task RunInstallAsync(Window owner)
    {
        var window = new DownloadFrpcWindow();
        await window.ShowDialog(owner);

        if (_viewModel is null) return;

        _viewModel.RefreshFrpcInfo();
        await _viewModel.RefreshFrpcVersionAsync();

        // Brings back tunnels stopped to free the binary; a no-op when none were.
        await _viewModel.RestoreTunnelsAsync(window.StoppedTunnelNames);
    }
}
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LoliaFrpClient.Services;
using LoliaFrpClient.ViewModels;

namespace LoliaFrpClient.Views;

public partial class SettingsPage : UserControl
{
    private SettingsViewModel? _viewModel;

    public SettingsPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.InstallFrpcRequested -= OnInstallFrpcRequested;
            _viewModel.PickFrpcPathRequested -= OnPickFrpcPathRequested;
            _viewModel.OpenFrpcManagerRequested -= OnOpenFrpcManagerRequested;
        }

        _viewModel = DataContext as SettingsViewModel;

        if (_viewModel is not null)
        {
            _viewModel.InstallFrpcRequested += OnInstallFrpcRequested;
            _viewModel.PickFrpcPathRequested += OnPickFrpcPathRequested;
            _viewModel.OpenFrpcManagerRequested += OnOpenFrpcManagerRequested;
        }
    }

    private async void OnInstallFrpcRequested()
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner) return;

            var window = new DownloadFrpcWindow();
            await window.ShowDialog(owner);

            // Re-read afterwards, otherwise this page still shows "未安装".
            _viewModel?.RefreshFrpcCore();
        }
        catch (Exception)
        {
        }
    }

    // Deliberately the only entry point for this. Putting it on the Frpc manager page would
    // invite changing the binary while tunnels are live, which affects only the next start.
    private async void OnPickFrpcPathRequested()
    {
        try
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null || _viewModel is null) return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择 frpc 可执行文件",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("frpc")
                    {
                        Patterns = [OperatingSystem.IsWindows() ? "frpc.exe" : "frpc"]
                    }
                ]
            });

            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;

            await _viewModel.SetFrpcPathAsync(path);
        }
        catch (Exception)
        {
        }
    }

    private async void OnOpenFrpcManagerRequested()
    {
        try
        {
            await ShellNavigation.NavigateAsync(this, "frpc");
        }
        catch (Exception)
        {
        }
    }
}
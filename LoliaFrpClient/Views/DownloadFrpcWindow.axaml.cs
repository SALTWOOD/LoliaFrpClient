using System.Collections.Generic;
using Avalonia.Controls;
using LoliaFrpClient.Core.Frpc;
using LoliaFrpClient.ViewModels;

namespace LoliaFrpClient.Views;

public partial class DownloadFrpcWindow : Window
{
    private readonly DownloadFrpcViewModel _viewModel;

    public DownloadFrpcWindow()
    {
        InitializeComponent();

        _viewModel = new DownloadFrpcViewModel();
        DataContext = _viewModel;

        _viewModel.CloseRequested += Close;
        Opened += async (_, _) => await _viewModel.StartAsync();
        Closed += (_, _) => _viewModel.Dispose();
    }

    public IReadOnlyList<string> StoppedTunnelNames => _viewModel.StoppedTunnelNames;

    public FrpcInstallResult? Result => _viewModel.Result;
}
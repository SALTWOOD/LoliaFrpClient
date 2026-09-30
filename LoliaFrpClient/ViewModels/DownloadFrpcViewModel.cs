using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoliaFrpClient.Core.Frpc;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.ViewModels;

public sealed partial class DownloadFrpcViewModel : ObservableObject, IDisposable
{
    private readonly HttpClient _http;
    private readonly FrpcInstaller _installer;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public DownloadFrpcViewModel()
    {
        _http = new HttpClient();

        // Required, not optional: the GitHub API answers 403 without a User-Agent.
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "LoliaFrpClient");
        _installer = new FrpcInstaller(_http);
    }

    public event Action? CloseRequested;

    // Tunnels stopped to free the binary; the caller restarts them once the window closes.
    public IReadOnlyList<string> StoppedTunnelNames { get; private set; } = [];

    public FrpcInstallResult? Result { get; private set; }

    [ObservableProperty] public partial bool IsIndeterminate { get; set; } = true;

    [ObservableProperty] public partial double ProgressValue { get; set; }

    [ObservableProperty] public partial string ProgressText { get; set; } = string.Empty;

    [ObservableProperty] public partial string SpeedText { get; set; } = string.Empty;

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool CanCancel { get; set; }

    [ObservableProperty] public partial bool CanClose { get; set; }

    public async Task StartAsync()
    {
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        CanCancel = true;
        CanClose = false;
        IsIndeterminate = true;
        ProgressValue = 0;
        ProgressText = string.Empty;
        SpeedText = string.Empty;
        StatusText = "正在准备…";

        try
        {
            // Free the executable first; overwriting a running exe fails on Windows. Stopping can
            // block for seconds, so keep it off the UI thread.
            var running = FrpcProcessManager.Current.SnapshotLocalStates().Keys.ToList();
            if (running.Count > 0)
            {
                StatusText = "正在停止本客户端启动的隧道…";
                await Task.Run(() => FrpcProcessManager.Current.StopAll(), ct).ConfigureAwait(true);
                StoppedTunnelNames = running;
            }

            var options = new FrpcInstallOptions
            {
                UseMirror = AppSettings.Current.UseDownloadMirror
            };

            // Constructed here so it captures the UI thread's synchronization context.
            var progress = new Progress<FrpcInstallProgress>(ApplyProgress);
            Result = await _installer.InstallAsync(options, progress, ct).ConfigureAwait(true);

            if (Result.Canceled)
            {
                StatusText = "已取消";
            }
            else if (!Result.Success)
            {
                StatusText = $"失败:{Result.Message}";
            }
            else
            {
                AppSettings.Current.FrpcPath = Result.FrpcPath;
                AppSettings.Current.FrpcVersion = Result.Version;
                AppSettings.Current.FrpcLastUpdateCheckUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                AppSettings.Current.Save();

                IsIndeterminate = false;
                ProgressValue = 100;
                ProgressText = "完成";
                SpeedText = string.Empty;
                StatusText = string.IsNullOrWhiteSpace(Result.Version)
                    ? "安装完成"
                    : $"已安装 frpc {Result.Version}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            StatusText = $"失败:{ex.Message}";
        }
        finally
        {
            CanCancel = false;
            CanClose = true;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (!CanCancel) return;

        CanCancel = false;
        StatusText = "正在取消…";

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _cts.Dispose();
        _http.Dispose();
    }

    private void ApplyProgress(FrpcInstallProgress progress)
    {
        if (!string.IsNullOrWhiteSpace(progress.Message)) StatusText = progress.Message;

        switch (progress.Stage)
        {
            case FrpcInstallStage.Downloading:
                // Without a Content-Length there is no meaningful percentage.
                IsIndeterminate = progress.TotalBytes <= 0;
                if (progress.ReceivedBytes <= 0) break;

                ProgressValue = progress.Percent;
                ProgressText = progress.TotalBytes > 0
                    ? $"{ByteSize.Format(progress.ReceivedBytes)} / {ByteSize.Format(progress.TotalBytes)} ({progress.Percent:F1}%)"
                    : ByteSize.Format(progress.ReceivedBytes);
                SpeedText = progress.SpeedBytesPerSecond > 0
                    ? $"速度 {ByteSize.Format((long)progress.SpeedBytesPerSecond)}/s"
                    : string.Empty;
                break;

            case FrpcInstallStage.Completed:
                IsIndeterminate = false;
                ProgressValue = 100;
                ProgressText = "完成";
                SpeedText = string.Empty;
                break;

            default:
                IsIndeterminate = true;
                break;
        }
    }
}
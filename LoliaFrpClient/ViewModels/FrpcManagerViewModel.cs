using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoliaFrpClient.Core;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.ViewModels;

public sealed partial class FrpcManagerViewModel : ViewModelBase, IDisposable
{
    private const int MaxDisplayLines = 600;

    private const int TrimToLines = 500;

    private const int DrainBatchSize = 500;

    private const int MaxLogTabs = 12;

    private readonly DispatcherTimer _logTimer;
    private readonly FrpcProcessManager _manager = FrpcProcessManager.Current;
    private bool _disposed;

    // Gates OnUseDownloadMirrorChanged so construction does not emit a status message.
    private bool _ready;

    private FrpcTunnelRow? _pendingStart;

    public FrpcManagerViewModel()
    {
        _logTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _logTimer.Tick += (_, _) => FlushLog();
        _logTimer.Start();

        _manager.StateChanged += OnProcessStateChanged;

        RefreshFrpcInfo();
        UseDownloadMirror = AppSettings.Current.UseDownloadMirror;
        _ready = true;
    }

    public ObservableCollection<FrpcTunnelRow> Tunnels { get; } = [];

    // One tab per tunnel so a busy tunnel does not drown out the others.
    public ObservableCollection<FrpcLogTab> LogTabs { get; } = [];

    [ObservableProperty] public partial FrpcLogTab? SelectedLogTab { get; set; }

    public event Action? InstallFrpcRequested;

    public event Action? FrpcMissingRequested;

    // 内置 frpc 时换不了也用不着换,页面据此藏掉「安装 / 更新」。
    public bool CanInstallFrpc => !FrpcPath.IsBundled;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrpcVersionText))]
    public partial string? FrpcVersion { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrpcVersionText))]
    public partial bool IsFrpcReady { get; set; }

    [ObservableProperty] public partial bool UseDownloadMirror { get; set; }

    public string FrpcVersionText => IsFrpcReady ? FrpcVersion ?? "未知" : "未安装";

    public bool IsEmpty => Tunnels.Count == 0 && ErrorMessage is null;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasLogTabs => LogTabs.Count > 0;

    public override async Task ActivateAsync()
    {
        RefreshFrpcInfo();
        SelectedLogTab ??= LogTabs.FirstOrDefault();

        // Only probe when the version is still unknown, so revisiting the page does not
        // spawn an frpc process every time.
        if (IsFrpcReady && FrpcVersion is null) await RefreshFrpcVersionAsync().ConfigureAwait(true);

        await LoadAsync().ConfigureAwait(true);
    }

    public void RefreshFrpcInfo()
    {
        var path = FrpcPath.Current;
        IsFrpcReady = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        FrpcVersion = IsFrpcReady ? AppSettings.Current.FrpcVersion : null;
    }

    public async Task RefreshFrpcVersionAsync(CancellationToken ct = default)
    {
        var path = FrpcPath.Current;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            RefreshFrpcInfo();
            return;
        }

        var version = await FrpcVersionProbe.TryReadAsync(path, ct).ConfigureAwait(true);
        if (version is not null)
        {
            AppSettings.Current.FrpcVersion = version;
            AppSettings.Current.Save();
        }

        RefreshFrpcInfo();
    }

    public void CancelPendingStart()
    {
        _pendingStart = null;
        StatusMessage = null;
    }

    // No-op when nothing was pending, so callers can invoke it unconditionally.
    public async Task ResumePendingStartAsync()
    {
        var row = _pendingStart;
        _pendingStart = null;

        if (row is null) return;

        var current = Tunnels.FirstOrDefault(t => string.Equals(t.Name, row.Name, StringComparison.Ordinal)) ?? row;
        await StartRowAsync(current).ConfigureAwait(true);
    }

    // Restarts the tunnels that were stopped to free the binary during an install.
    // The delay is required: the server only reports inactive once it notices the disconnect,
    // and until then the gating rule refuses to start them.
    public async Task RestoreTunnelsAsync(IReadOnlyList<string> tunnelNames)
    {
        if (tunnelNames.Count == 0) return;

        StatusMessage = "正在恢复被中断的隧道…";
        await Task.Delay(TimeSpan.FromSeconds(4)).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);

        var restored = 0;
        foreach (var name in tunnelNames)
        {
            var row = Tunnels.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
            if (row is null || !row.CanStart) continue;

            await StartRowAsync(row).ConfigureAwait(true);
            restored++;
        }

        StatusMessage = restored > 0
            ? $"已恢复 {restored} 条被中断的隧道"
            : "被中断的隧道未能自动恢复,请手动启动";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            if (!ApiSession.Current.IsAuthenticated)
            {
                Tunnels.Clear();
                ErrorMessage = "尚未登录,请前往「设置」完成登录。";
                return;
            }

            var result = await Tunnel.ListAsync().ConfigureAwait(true);

            if (result is { IsSuccess: true, Data: { } tunnels })
                Merge(tunnels);
            else
                ErrorMessage = result.Msg;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            NotifyCommandStates();
        }
    }

    [RelayCommand]
    private Task StartAsync(FrpcTunnelRow? row)
    {
        return row is null ? Task.CompletedTask : StartRowAsync(row);
    }

    public async Task StartRowAsync(FrpcTunnelRow row)
    {
        if (!row.CanStart)
        {
            StatusMessage = $"{row.DisplayName}:当前状态不允许启动";
            return;
        }

        RefreshFrpcInfo();
        if (!IsFrpcReady)
        {
            _pendingStart = row;
            StatusMessage = $"{row.DisplayName}:尚未安装 frpc";
            FrpcMissingRequested?.Invoke();
            return;
        }

        var frpcPath = FrpcPath.Current!;
        StatusMessage = null;

        // The token only comes back from the per-tunnel detail endpoint, so this is one
        // request per tunnel rather than a single batch call.
        var credential = await new Tunnel(row.Name).GetLaunchCredentialAsync().ConfigureAwait(true);
        if (!credential.IsSuccess || credential.Data is null)
        {
            StatusMessage = $"获取隧道凭据失败:{credential.Msg}";
            return;
        }

        var started = _manager.TryStart(row.Name, credential.Data.TunnelId, credential.Data.Token, frpcPath);
        if (!started.Success)
        {
            StatusMessage = $"{row.DisplayName}:{started.Message}";
            return;
        }

        row.LocalState = FrpcTunnelState.Starting;
        Recompute(row);
        StatusMessage = $"{row.DisplayName}:已启动";
        EnsureLogTab(row);

        ScheduleServerRefresh();
    }

    [RelayCommand]
    private async Task StopAsync(FrpcTunnelRow? row)
    {
        if (row is null) return;

        if (!row.CanStop)
        {
            StatusMessage = $"{row.DisplayName}:只有本客户端启动的隧道才能停止";
            return;
        }

        // Stopping can block up to 3s waiting for the process to die.
        var stopped = await Task.Run(() => _manager.Stop(row.Name)).ConfigureAwait(true);

        if (!stopped)
        {
            StatusMessage = $"{row.DisplayName}:停止失败,该隧道不在本客户端运行";
            return;
        }

        row.LocalState = FrpcTunnelState.Offline;
        Recompute(row);
        StatusMessage = $"{row.DisplayName}:已停止";
        ScheduleServerRefresh();
    }

    [RelayCommand(CanExecute = nameof(CanStartAny))]
    private async Task StartAllAsync()
    {
        foreach (var row in Tunnels.Where(t => t.CanStart).ToList()) await StartRowAsync(row).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanStopAny))]
    private async Task StopAllAsync()
    {
        foreach (var row in Tunnels.Where(t => t.CanStop).ToList()) await StopAsync(row).ConfigureAwait(true);
    }

    // Stop everything first, then start: interleaving makes old and new frpc processes
    // contend for the same executable.
    [RelayCommand(CanExecute = nameof(CanStopAny))]
    private async Task RestartAllAsync()
    {
        var targets = Tunnels.Where(t => t.CanStop).ToList();

        foreach (var row in targets) await StopAsync(row).ConfigureAwait(true);

        foreach (var row in targets) await StartRowAsync(row).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ClearLog()
    {
        var tab = SelectedLogTab;
        if (tab is null) return;

        _manager.GetLog(tab.TunnelName).Clear();
        tab.Lines.Clear();
    }

    [RelayCommand]
    private void InstallFrpc()
    {
        InstallFrpcRequested?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _manager.StateChanged -= OnProcessStateChanged;
        _logTimer.Stop();
    }

    partial void OnUseDownloadMirrorChanged(bool value)
    {
        if (!_ready) return;

        AppSettings.Current.UseDownloadMirror = value;
        AppSettings.Current.Save();
        StatusMessage = value ? "下载源:镜像加速" : "下载源:GitHub 直连";
    }

    private bool CanStartAny()
    {
        return Tunnels.Any(t => t.CanStart);
    }

    private bool CanStopAny()
    {
        return Tunnels.Any(t => t.CanStop);
    }

    private void NotifyCommandStates()
    {
        StartAllCommand.NotifyCanExecuteChanged();
        StopAllCommand.NotifyCanExecuteChanged();
        RestartAllCommand.NotifyCanExecuteChanged();
    }

    // Updates rows in place instead of rebuilding: a full rebuild makes every running tunnel
    // flicker back to "stopped" for a moment.
    private void Merge(IReadOnlyList<Tunnel> tunnels)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tunnel in tunnels)
        {
            var summary = tunnel.Summary;
            var name = summary?.Name ?? tunnel.Name;
            if (string.IsNullOrWhiteSpace(name)) continue;

            seen.Add(name);
            var serverActive = string.Equals(summary?.Status, "active", StringComparison.OrdinalIgnoreCase);

            var row = Tunnels.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
            if (row is null)
            {
                row = new FrpcTunnelRow(name, summary?.Remark, summary?.RemotePort);
                Tunnels.Add(row);
            }

            row.ServerActive = serverActive;

            var tab = LogTabs.FirstOrDefault(t => string.Equals(t.TunnelName, name, StringComparison.Ordinal));
            if (tab is not null) tab.DisplayName = row.DisplayName;

            Recompute(row);
        }

        for (var i = Tunnels.Count - 1; i >= 0; i--)
            if (!seen.Contains(Tunnels[i].Name))
                Tunnels.RemoveAt(i);
    }

    // The gating rule lives here and nowhere else. Order matters:
    //   1. We hold the process -> trust the local state; the server's status lags.
    //   2. A local process just died -> keep Error so the user knows to retry.
    //   3. Server says up, we hold nothing -> OnlineRemote. frpc logins for one tunnel are
    //      mutually exclusive, so offering "start" here would kick whoever is using it.
    private void Recompute(FrpcTunnelRow row)
    {
        var isLocal = _manager.IsLocal(row.Name);

        if (isLocal) row.LocalState = _manager.GetLocalState(row.Name);

        row.State = Resolve(row.ServerActive, row.LocalState, isLocal);

        row.CanStart = row.State is FrpcTunnelState.Offline or FrpcTunnelState.Error;
        row.CanStop = isLocal;

        row.Hint = row.CanStart || row.CanStop
            ? null
            : "该隧道正由其他设备或客户端使用。frpc 对同一隧道的重复登录会互相踢下线,因此本客户端不能启动或停止它。";

        NotifyCommandStates();
    }

    private static FrpcTunnelState Resolve(bool serverActive, FrpcTunnelState localState, bool isLocal)
    {
        if (isLocal)
            return localState is FrpcTunnelState.Online or FrpcTunnelState.Starting
                ? localState
                : FrpcTunnelState.Starting;

        if (localState == FrpcTunnelState.Error) return FrpcTunnelState.Error;

        return serverActive ? FrpcTunnelState.OnlineRemote : FrpcTunnelState.Offline;
    }

    private void OnProcessStateChanged(string tunnelName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;

            var row = Tunnels.FirstOrDefault(t => string.Equals(t.Name, tunnelName, StringComparison.Ordinal));
            if (row is null) return;

            if (!_manager.IsLocal(tunnelName) &&
                row.LocalState is FrpcTunnelState.Online or FrpcTunnelState.Starting)
                // Gone without us stopping it; a requested stop sets Offline in StopAsync.
                row.LocalState = FrpcTunnelState.Error;

            Recompute(row);
        });
    }

    // The server's status only converges once frpc has connected, so refresh shortly after.
    private void ScheduleServerRefresh()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(LoadAsync).ConfigureAwait(false);
        });
    }

    private FrpcLogTab EnsureLogTab(FrpcTunnelRow row)
    {
        var tab = LogTabs.FirstOrDefault(t => string.Equals(t.TunnelName, row.Name, StringComparison.Ordinal));
        if (tab is not null) return tab;

        if (LogTabs.Count >= MaxLogTabs)
        {
            var evictable = LogTabs.FirstOrDefault(t =>
                !ReferenceEquals(t, SelectedLogTab) && !_manager.IsLocal(t.TunnelName));

            if (evictable is not null) LogTabs.Remove(evictable);
        }

        tab = new FrpcLogTab(row.Name, row.DisplayName);
        LogTabs.Add(tab);
        OnPropertyChanged(nameof(HasLogTabs));

        // Do not steal focus from a tab the user is already reading.
        SelectedLogTab ??= tab;
        return tab;
    }

    private void FlushLog()
    {
        if (_disposed) return;

        // Only the visible tab is drained. Hidden ones keep filling their bounded buffer and
        // stop at the cap; draining them all would spend memory on logs nobody is reading.
        var tab = SelectedLogTab;
        if (tab is null) return;

        var batch = _manager.GetLog(tab.TunnelName).Drain(DrainBatchSize);
        if (batch.Count == 0) return;

        foreach (var line in batch) tab.Lines.Add(line);

        if (tab.Lines.Count <= MaxDisplayLines) return;

        var excess = tab.Lines.Count - TrimToLines;
        for (var i = 0; i < excess; i++) tab.Lines.RemoveAt(0);
    }
}

// Like the row, this stays a dumb holder: the ViewModel owns all state derivation.
public sealed partial class FrpcLogTab : ObservableObject
{
    public FrpcLogTab(string tunnelName, string displayName)
    {
        TunnelName = tunnelName;
        DisplayName = displayName;
    }

    public string TunnelName { get; }

    [ObservableProperty] public partial string DisplayName { get; set; }

    public ObservableCollection<string> Lines { get; } = [];
}

public sealed partial class FrpcTunnelRow : ObservableObject
{
    public FrpcTunnelRow(string name, string? remark, int? remotePort)
    {
        Name = name;
        Remark = remark;
        RemotePort = remotePort;
    }

    public string Name { get; }

    public string? Remark { get; }

    public int? RemotePort { get; }

    public bool ServerActive { get; set; }

    public FrpcTunnelState LocalState { get; set; } = FrpcTunnelState.Offline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnline))]
    [NotifyPropertyChangedFor(nameof(IsOffline))]
    [NotifyPropertyChangedFor(nameof(IsStarting))]
    [NotifyPropertyChangedFor(nameof(IsError))]
    [NotifyPropertyChangedFor(nameof(IsRemote))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial FrpcTunnelState State { get; set; }

    [ObservableProperty] public partial bool CanStart { get; set; }

    [ObservableProperty] public partial bool CanStop { get; set; }

    [ObservableProperty] public partial string? Hint { get; set; }

    // The tunnel name is a random string, so the remark reads far better as a title.
    public string DisplayName => string.IsNullOrWhiteSpace(Remark) ? Name : Remark!;

    public string StatusText => State switch
    {
        FrpcTunnelState.Online => "运行中",
        FrpcTunnelState.Starting => "启动中",
        FrpcTunnelState.OnlineRemote => "在线 · 其他设备",
        FrpcTunnelState.Error => "异常",
        _ => "已下线"
    };

    public string RemotePortText => RemotePort is > 0 ? RemotePort.Value.ToString() : "—";

    // Drives Classes.xxx bindings so no value converter is needed.
    public bool IsOnline => State == FrpcTunnelState.Online;

    public bool IsOffline => State == FrpcTunnelState.Offline;

    public bool IsStarting => State == FrpcTunnelState.Starting;

    public bool IsError => State == FrpcTunnelState.Error;

    public bool IsRemote => State == FrpcTunnelState.OnlineRemote;
}
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LoliaFrpClient.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace LoliaFrpClient.Pages;

/// <summary>
///     用于显示的进程项
/// </summary>
public class FrpcProcessDisplayItem : INotifyPropertyChanged
{
    private bool _isRunning;
    private Brush _statusColor = new SolidColorBrush(Colors.Gray);
    private string _statusText = string.Empty;

    public int TunnelId { get; set; }
    public string TunnelName { get; set; } = string.Empty;
    public string TunnelRemark { get; set; } = string.Empty;
    public FrpcProcessInfo? ProcessInfo { get; set; }

    /// <summary>
    ///     显示名称：优先显示 remark，如果为空则显示 name
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(TunnelRemark) ? TunnelName : TunnelRemark;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            OnPropertyChanged();
            UpdateStatus();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public Brush StatusColor
    {
        get => _statusColor;
        set
        {
            _statusColor = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void UpdateStatus()
    {
        if (IsRunning)
        {
            StatusText = "运行中";
            StatusColor = new SolidColorBrush(Colors.Green);
        }
        else
        {
            StatusText = "已停止";
            StatusColor = new SolidColorBrush(Colors.Red);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
///     Frpc 管理页面
/// </summary>
public sealed partial class FrpcManagerPage : Page, INotifyPropertyChanged
{
    /// <summary>
    ///     日志刷新节流间隔。frpc 在高流量下每秒可产生数百行输出，
    ///     若每行都推送到 UI 线程并重排 TextBlock，会造成 DispatcherQueue 与
    ///     字符串分配的无界增长（此前报告过的 64GB 级内存占用）。
    /// </summary>
    private static readonly TimeSpan LogFlushInterval = TimeSpan.FromMilliseconds(250);

    private readonly FrpcManager _frpcManager;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _logFlushTimer;
    private readonly StringBuilder _logBuilder = new();

    /// <summary>待渲染的日志行（含所属隧道 ID）。后台线程入队，UI 线程按帧批量取出。</summary>
    private readonly ConcurrentQueue<(int TunnelId, string LogLine)> _pendingLogs = new();

    /// <summary>待渲染的行数上限，超出时丢弃最旧的，保证积压有界。</summary>
    private const int MaxPendingLogs = 4096;

    private FrpcProcessDisplayItem? _selectedProcess;

    public FrpcManagerPage()
    {
        InitializeComponent();
        _frpcManager = ServiceLocator.FrpcManager;

        // 设置定时器刷新状态
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += OnRefreshTimerTick;

        // 日志按帧批量刷新，避免逐行重排 UI
        _logFlushTimer = new DispatcherTimer { Interval = LogFlushInterval };
        _logFlushTimer.Tick += OnLogFlushTick;

        // 页面被 NavigationCacheMode 缓存时会重入，因此订阅与启动都放在 Loaded/Unloaded 成对处理。
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // 先退订再订阅，避免重复进入时挂载多份处理器
        _frpcManager.TunnelProcessStarted -= OnTunnelProcessStarted;
        _frpcManager.TunnelProcessExited -= OnTunnelProcessExited;
        _frpcManager.TunnelProcessLogAdded -= OnTunnelProcessLogAdded;

        _frpcManager.TunnelProcessStarted += OnTunnelProcessStarted;
        _frpcManager.TunnelProcessExited += OnTunnelProcessExited;
        _frpcManager.TunnelProcessLogAdded += OnTunnelProcessLogAdded;

        _refreshTimer.Start();
        _logFlushTimer.Start();

        LoadRunningProcesses();
    }

    /// <summary>
    ///     运行中实例的窗口。frpc 退出后立即从窗口移除，避免长期驻留。
    /// </summary>
    public ObservableCollection<FrpcProcessDisplayItem> RunningProcesses { get; } = new();

    public FrpcProcessDisplayItem? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            _selectedProcess = value;
            OnPropertyChanged();
            UpdateDetailView();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void LoadRunningProcesses()
    {
        RunningProcesses.Clear();

        var processes = _frpcManager.GetAllProcesses();
        foreach (var process in processes)
        {
            if (!process.IsRunning) continue;

            var item = new FrpcProcessDisplayItem
            {
                TunnelId = process.TunnelId,
                TunnelName = process.TunnelName,
                TunnelRemark = process.TunnelRemark,
                ProcessInfo = process,
                IsRunning = true
            };
            RunningProcesses.Add(item);
        }

        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyStateText.Visibility = RunningProcesses.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnTunnelProcessStarted(object? sender, FrpcProcessInfo info)
    {
        // 在 UI 线程上更新
        DispatcherQueue.TryEnqueue(() =>
        {
            var existing = RunningProcesses.FirstOrDefault(p => p.TunnelId == info.TunnelId);
            if (existing == null)
            {
                RunningProcesses.Add(new FrpcProcessDisplayItem
                {
                    TunnelId = info.TunnelId,
                    TunnelName = info.TunnelName,
                    TunnelRemark = info.TunnelRemark,
                    ProcessInfo = info,
                    IsRunning = true
                });
            }
            else
            {
                existing.ProcessInfo = info;
                existing.IsRunning = true;
            }

            UpdateEmptyState();
        });
    }

    private void OnTunnelProcessExited(object? sender, FrpcProcessInfo info)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var item = RunningProcesses.FirstOrDefault(p => p.TunnelId == info.TunnelId);
            if (item != null)
            {
                item.IsRunning = false;

                // 进程已释放，清空对 Process 的引用并移出列表，
                // 否则已退出的进程及其日志会一直驻留内存。
                item.ProcessInfo = null;

                if (SelectedProcess == item)
                {
                    SelectedProcess = null;
                    UpdateDetailView();
                }

                RunningProcesses.Remove(item);
            }

            UpdateEmptyState();
        });
    }

    private void OnTunnelProcessLogAdded(object? sender, (int TunnelId, string LogLine) args)
    {
        // 事件在后台线程（stdout/stderr 读取线程）上触发，不得触碰任何 UI 对象。
        // 只做入队，由 UI 线程上的 _logFlushTimer 批量渲染。
        _pendingLogs.Enqueue((args.TunnelId, args.LogLine));

        // 丢弃最旧的积压，保证队列有界
        while (_pendingLogs.Count > MaxPendingLogs) _pendingLogs.TryDequeue(out _);
    }

    /// <summary>
    ///     UI 线程：按帧批量取出待渲染日志并一次性更新文本。
    ///     相比每行一次 string.Join + TextBlock 重排，字符串分配与布局次数降低数百倍。
    /// </summary>
    private void OnLogFlushTick(object? sender, object e)
    {
        if (_pendingLogs.IsEmpty) return;

        var selectedId = _selectedProcess?.TunnelId;
        var added = false;

        while (_pendingLogs.TryDequeue(out var entry))
        {
            // 只渲染当前选中实例的日志；切换/清空后自动从空文本重新累积
            if (selectedId == null || entry.TunnelId != selectedId) continue;

            if (!added)
            {
                if (LogOutputText.Text.Length > _logBuilder.Length ||
                    !LogOutputText.Text.StartsWith(_logBuilder.ToString(), StringComparison.Ordinal))
                    _logBuilder.Clear();
                added = true;
            }

            _logBuilder.Append(entry.LogLine).Append('\n');
        }

        if (!added) return;

        var text = _logBuilder.ToString();

        // 只保留最近 MaxLogLines 行，保证内存与布局开销有界
        var excess = CountLines(text) - MaxLogLines;
        if (excess > 0)
        {
            text = TrimLeadingLines(text, excess);
            _logBuilder.Clear();
            _logBuilder.Append(text);
        }

        LogOutputText.Text = text;

        // 滚动到底部
        LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
    }

    private const int MaxLogLines = 500;

    private static int CountLines(string text)
    {
        var count = 0;
        foreach (var c in text)
            if (c == '\n')
                count++;
        return count + 1;
    }

    private static string TrimLeadingLines(string text, int linesToRemove)
    {
        var index = -1;
        for (var i = 0; i < linesToRemove; i++)
        {
            index = text.IndexOf('\n', index + 1);
            if (index < 0) return string.Empty;
        }

        return text[(index + 1)..];
    }

    private void OnRefreshTimerTick(object? sender, object e)
    {
        // 刷新所有进程状态
        foreach (var item in RunningProcesses)
            if (item.ProcessInfo != null)
                item.IsRunning = item.ProcessInfo.IsRunning;

        // 更新运行时长
        if (SelectedProcess?.ProcessInfo != null)
        {
            var runningTime = DateTime.Now - SelectedProcess.ProcessInfo.StartTime;
            RunningTimeText.Text = FormatRunningTime(runningTime);
        }
    }

    private void OnProcessSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProcessListView.SelectedItem is FrpcProcessDisplayItem item)
            SelectedProcess = item;
        else
            SelectedProcess = null;
    }

    private void UpdateDetailView()
    {
        // 切换实例时丢弃上一个实例的待渲染日志
        while (_pendingLogs.TryDequeue(out _)) { }
        _logBuilder.Clear();

        if (SelectedProcess == null || SelectedProcess.ProcessInfo == null)
        {
            DetailTitle.Text = "选择一个实例查看详情";
            ControlButtons.Visibility = Visibility.Collapsed;
            ProcessInfoPanel.Visibility = Visibility.Collapsed;
            LogOutputText.Text = "选择一个实例查看日志输出";
            return;
        }

        var info = SelectedProcess.ProcessInfo;
        // 优先显示 remark，如果为空则显示 name
        DetailTitle.Text = string.IsNullOrWhiteSpace(info.TunnelRemark) ? info.TunnelName : info.TunnelRemark;
        ControlButtons.Visibility = Visibility.Visible;
        ProcessInfoPanel.Visibility = Visibility.Visible;

        ProcessIdText.Text = info.IsRunning ? info.Process.Id.ToString() : "已退出";
        StartTimeText.Text = info.StartTime.ToString("HH:mm:ss");

        var runningTime = DateTime.Now - info.StartTime;
        RunningTimeText.Text = FormatRunningTime(runningTime);

        // 从 ProcessInfo 加载历史日志
        foreach (var log in info.GetLogSnapshot()) _logBuilder.Append(log).Append('\n');
        LogOutputText.Text = _logBuilder.Length == 0 ? "暂无日志输出" : _logBuilder.ToString();
    }

    private async void OnRestartClick(object sender, RoutedEventArgs e)
    {
        if (SelectedProcess == null) return;

        try
        {
            _frpcManager.Restart(SelectedProcess.TunnelId);
            while (_pendingLogs.TryDequeue(out _)) { }
            _logBuilder.Clear();
            LogOutputText.Text = "暂无日志输出";
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("重启失败", ex.Message);
        }
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        if (SelectedProcess == null) return;

        try
        {
            _frpcManager.Stop(SelectedProcess.TunnelId);
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("停止失败", ex.Message);
        }
    }

    private void OnClearLogClick(object sender, RoutedEventArgs e)
    {
        while (_pendingLogs.TryDequeue(out _)) { }
        _logBuilder.Clear();
        LogOutputText.Text = "暂无日志输出";
    }

    private string FormatRunningTime(TimeSpan time)
    {
        if (time.TotalSeconds < 60)
            return $"{(int)time.TotalSeconds} 秒";
        if (time.TotalMinutes < 60)
            return $"{(int)time.TotalMinutes} 分 {time.Seconds} 秒";
        if (time.TotalHours < 24)
            return $"{(int)time.TotalHours} 小时 {time.Minutes} 分";
        return $"{(int)time.TotalDays} 天 {time.Hours} 小时";
    }

    private async Task ShowErrorDialogAsync(string title, string message)
    {
        await DialogManager.Instance.ShowErrorAsync(title, message);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        // 注意：只停止计时器，不解除 Tick 订阅 —— 页面可能被重新加载，
        // 届时 Page_Loaded 会再次 Start 这两个计时器。
        _refreshTimer.Stop();
        _logFlushTimer.Stop();

        while (_pendingLogs.TryDequeue(out _)) { }
        _logBuilder.Clear();

        _frpcManager.TunnelProcessStarted -= OnTunnelProcessStarted;
        _frpcManager.TunnelProcessExited -= OnTunnelProcessExited;
        _frpcManager.TunnelProcessLogAdded -= OnTunnelProcessLogAdded;
    }
}
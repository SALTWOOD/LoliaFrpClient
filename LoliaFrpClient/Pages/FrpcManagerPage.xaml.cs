using LoliaFrpClient.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LoliaFrpClient.Pages
{
    /// <summary>
    /// 用于显示的进程项
    /// </summary>
    public class FrpcProcessDisplayItem : INotifyPropertyChanged
    {
        private bool _isRunning;
        private string _statusText = string.Empty;
        private Brush _statusColor = new SolidColorBrush(Microsoft.UI.Colors.Gray);

        public int TunnelId { get; set; }
        public string TunnelName { get; set; } = string.Empty;
        public FrpcProcessInfo? ProcessInfo { get; set; }

        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); UpdateStatus(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public Brush StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        private void UpdateStatus()
        {
            if (IsRunning)
            {
                StatusText = "运行中";
                StatusColor = new SolidColorBrush(Microsoft.UI.Colors.Green);
            }
            else
            {
                StatusText = "已停止";
                StatusColor = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Frpc 管理页面
    /// </summary>
    public sealed partial class FrpcManagerPage : Page, INotifyPropertyChanged
    {
        private readonly FrpcManager _frpcManager;
        private FrpcProcessDisplayItem? _selectedProcess;
        private readonly ObservableCollection<string> _currentLogs = new ObservableCollection<string>();
        private readonly DispatcherTimer _refreshTimer;

        public ObservableCollection<FrpcProcessDisplayItem> RunningProcesses { get; } = new ObservableCollection<FrpcProcessDisplayItem>();

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

        public FrpcManagerPage()
        {
            this.InitializeComponent();
            _frpcManager = ServiceLocator.FrpcManager;
            
            // 订阅事件
            _frpcManager.TunnelProcessStarted += OnTunnelProcessStarted;
            _frpcManager.TunnelProcessExited += OnTunnelProcessExited;
            _frpcManager.TunnelProcessLogAdded += OnTunnelProcessLogAdded;

            // 设置定时器刷新状态
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _refreshTimer.Tick += OnRefreshTimerTick;
            _refreshTimer.Start();

            // 初始加载
            LoadRunningProcesses();
        }

        private void LoadRunningProcesses()
        {
            RunningProcesses.Clear();
            
            var processes = _frpcManager.GetAllProcesses();
            foreach (var process in processes)
            {
                var item = new FrpcProcessDisplayItem
                {
                    TunnelId = process.TunnelId,
                    TunnelName = process.TunnelName,
                    ProcessInfo = process,
                    IsRunning = process.IsRunning
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
                    
                    // 如果选中的是这个进程，更新详情
                    if (SelectedProcess?.TunnelId == info.TunnelId)
                    {
                        UpdateDetailView();
                    }
                }
            });
        }

        private void OnTunnelProcessLogAdded(object? sender, (int TunnelId, string LogLine) args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // 如果当前选中的是这个进程，添加日志
                if (SelectedProcess?.TunnelId == args.TunnelId)
                {
                    _currentLogs.Add(args.LogLine);
                    
                    // 限制日志行数
                    while (_currentLogs.Count > 500)
                    {
                        _currentLogs.RemoveAt(0);
                    }
                    
                    UpdateLogDisplay();
                }
            });
        }

        private void OnRefreshTimerTick(object? sender, object e)
        {
            // 刷新所有进程状态
            foreach (var item in RunningProcesses)
            {
                if (item.ProcessInfo != null)
                {
                    item.IsRunning = item.ProcessInfo.IsRunning;
                }
            }

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
            {
                SelectedProcess = item;
            }
            else
            {
                SelectedProcess = null;
            }
        }

        private void UpdateDetailView()
        {
            if (SelectedProcess == null || SelectedProcess.ProcessInfo == null)
            {
                DetailTitle.Text = "选择一个实例查看详情";
                ControlButtons.Visibility = Visibility.Collapsed;
                ProcessInfoPanel.Visibility = Visibility.Collapsed;
                LogOutputText.Text = "选择一个实例查看日志输出";
                _currentLogs.Clear();
                return;
            }

            var info = SelectedProcess.ProcessInfo;
            DetailTitle.Text = info.TunnelName;
            ControlButtons.Visibility = Visibility.Visible;
            ProcessInfoPanel.Visibility = Visibility.Visible;

            ProcessIdText.Text = info.IsRunning ? info.Process.Id.ToString() : "已退出";
            StartTimeText.Text = info.StartTime.ToString("HH:mm:ss");
            
            var runningTime = DateTime.Now - info.StartTime;
            RunningTimeText.Text = FormatRunningTime(runningTime);

            // 从 ProcessInfo 加载历史日志
            _currentLogs.Clear();
            foreach (var log in info.LogOutput)
            {
                _currentLogs.Add(log);
            }
            
            UpdateLogDisplay();
        }

        private void UpdateLogDisplay()
        {
            if (_currentLogs.Count == 0)
            {
                LogOutputText.Text = "暂无日志输出";
            }
            else
            {
                LogOutputText.Text = string.Join("\n", _currentLogs);
                
                // 滚动到底部
                LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
            }
        }

        private async void OnRestartClick(object sender, RoutedEventArgs e)
        {
            if (SelectedProcess == null) return;

            try
            {
                _frpcManager.RestartTunnelProcess(SelectedProcess.TunnelId);
                _currentLogs.Clear();
                UpdateLogDisplay();
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
                _frpcManager.StopTunnelProcess(SelectedProcess.TunnelId);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("停止失败", ex.Message);
            }
        }

        private void OnClearLogClick(object sender, RoutedEventArgs e)
        {
            _currentLogs.Clear();
            UpdateLogDisplay();
        }

        private string FormatRunningTime(TimeSpan time)
        {
            if (time.TotalSeconds < 60)
                return $"{(int)time.TotalSeconds} 秒";
            if (time.TotalMinutes < 60)
                return $"{(int)time.TotalMinutes} 分 {(int)time.Seconds} 秒";
            if (time.TotalHours < 24)
                return $"{(int)time.TotalHours} 小时 {(int)time.Minutes} 分";
            return $"{(int)time.TotalDays} 天 {(int)time.Hours} 小时";
        }

        private async System.Threading.Tasks.Task ShowErrorDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
            _frpcManager.TunnelProcessStarted -= OnTunnelProcessStarted;
            _frpcManager.TunnelProcessExited -= OnTunnelProcessExited;
            _frpcManager.TunnelProcessLogAdded -= OnTunnelProcessLogAdded;
        }
    }
}

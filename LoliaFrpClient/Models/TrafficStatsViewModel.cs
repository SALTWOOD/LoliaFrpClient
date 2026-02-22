using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LoliaFrpClient.Models;

/// <summary>
///     流量统计视图模型
/// </summary>
public class TrafficStatsViewModel : INotifyPropertyChanged
{
    private long _trafficLimit;
    private long _trafficRemaining;
    private long _trafficUsed;
    private string _userId = string.Empty;
    private string _username = string.Empty;

    public string UserId
    {
        get => _userId;
        set
        {
            _userId = value;
            OnPropertyChanged();
        }
    }

    public string Username
    {
        get => _username;
        set
        {
            _username = value;
            OnPropertyChanged();
        }
    }

    public long TrafficLimit
    {
        get => _trafficLimit;
        set
        {
            _trafficLimit = value;
            OnPropertyChanged();
        }
    }

    public long TrafficUsed
    {
        get => _trafficUsed;
        set
        {
            _trafficUsed = value;
            OnPropertyChanged();
        }
    }

    public long TrafficRemaining
    {
        get => _trafficRemaining;
        set
        {
            _trafficRemaining = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     流量使用百分比
    /// </summary>
    public double TrafficUsagePercentage => TrafficLimit > 0 ? (double)TrafficUsed / TrafficLimit * 100 : 0;

    /// <summary>
    ///     格式化的流量限制
    /// </summary>
    public string FormattedTrafficLimit => Utils.FormatBytes(TrafficLimit);

    /// <summary>
    ///     格式化的已用流量
    /// </summary>
    public string FormattedTrafficUsed => Utils.FormatBytes(TrafficUsed);

    /// <summary>
    ///     格式化的剩余流量
    /// </summary>
    public string FormattedTrafficRemaining => Utils.FormatBytes(TrafficRemaining);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
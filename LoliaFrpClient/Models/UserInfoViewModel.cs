using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace LoliaFrpClient.Models;

/// <summary>
///     用户信息视图模型
/// </summary>
public class UserInfoViewModel : INotifyPropertyChanged
{
    private string _avatar = string.Empty;
    private int _bandwidthLimit;
    private string _createdAt = string.Empty;
    private string _email = string.Empty;
    private bool _hasKyc;
    private int _id;
    private bool _isBaned;
    private string _kycStatus = string.Empty;
    private int _maxTunnelCount;
    private string _role = string.Empty;
    private bool _todayChecked;
    private long _trafficLimit;
    private long _trafficUsed;
    private string _username = string.Empty;

    public int Id
    {
        get => _id;
        set
        {
            _id = value;
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

    public string Email
    {
        get => _email;
        set
        {
            _email = value;
            OnPropertyChanged();
        }
    }

    public string Avatar
    {
        get => _avatar;
        set
        {
            _avatar = value;
            OnPropertyChanged();
        }
    }

    public string Role
    {
        get => _role;
        set
        {
            _role = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RoleFormatted));
        }
    }

    public string KycStatus
    {
        get => _kycStatus;
        set
        {
            _kycStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(KycStatusFormatted));
            OnPropertyChanged(nameof(KycStatusColor));
            OnPropertyChanged(nameof(KycStatusBackgroundColor));
        }
    }

    public string CreatedAt
    {
        get => _createdAt;
        set
        {
            _createdAt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CreatedAtFormatted));
        }
    }

    public int MaxTunnelCount
    {
        get => _maxTunnelCount;
        set
        {
            _maxTunnelCount = value;
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
            OnPropertyChanged(nameof(TrafficLimitFormatted));
            OnPropertyChanged(nameof(TrafficRemainingFormatted));
            OnPropertyChanged(nameof(TrafficUsagePercentage));
        }
    }

    public long TrafficUsed
    {
        get => _trafficUsed;
        set
        {
            _trafficUsed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TrafficUsedFormatted));
            OnPropertyChanged(nameof(TrafficRemainingFormatted));
            OnPropertyChanged(nameof(TrafficUsagePercentage));
        }
    }

    public int BandwidthLimit
    {
        get => _bandwidthLimit;
        set
        {
            _bandwidthLimit = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BandwidthLimitFormatted));
        }
    }

    public bool HasKyc
    {
        get => _hasKyc;
        set
        {
            _hasKyc = value;
            OnPropertyChanged();
        }
    }

    public bool IsBaned
    {
        get => _isBaned;
        set
        {
            _isBaned = value;
            OnPropertyChanged();
        }
    }

    public bool TodayChecked
    {
        get => _todayChecked;
        set
        {
            _todayChecked = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     流量使用百分比
    /// </summary>
    public double TrafficUsagePercentage => TrafficLimit > 0 ? (double)TrafficUsed / TrafficLimit * 100 : 0;

    /// <summary>
    ///     剩余流量
    /// </summary>
    public long TrafficRemaining => TrafficLimit - TrafficUsed;

    /// <summary>
    ///     格式化流量限制显示（人类可读格式）
    /// </summary>
    public string TrafficLimitFormatted => Utils.FormatBytes(TrafficLimit);

    /// <summary>
    ///     格式化已用流量显示（人类可读格式）
    /// </summary>
    public string TrafficUsedFormatted => Utils.FormatBytes(TrafficUsed);

    /// <summary>
    ///     格式化剩余流量显示（人类可读格式）
    /// </summary>
    public string TrafficRemainingFormatted => Utils.FormatBytes(TrafficRemaining);

    /// <summary>
    ///     格式化创建时间显示（人类可读格式）
    /// </summary>
    public string CreatedAtFormatted => FormatDateTime(CreatedAt);

    /// <summary>
    ///     格式化带宽限制显示（人类可读格式）
    /// </summary>
    public string BandwidthLimitFormatted => FormatBandwidth(BandwidthLimit);

    /// <summary>
    ///     格式化权限显示（人类可读格式）
    /// </summary>
    public string RoleFormatted => FormatRole(Role);

    /// <summary>
    ///     格式化 KYC 状态显示（人类可读格式）
    /// </summary>
    public string KycStatusFormatted => FormatKycStatus(KycStatus);

    /// <summary>
    ///     获取 KYC 状态对应的文本颜色
    /// </summary>
    public string KycStatusColor => GetKycStatusColor(KycStatus);

    /// <summary>
    ///     获取 KYC 状态对应的背景色
    /// </summary>
    public string KycStatusBackgroundColor => GetKycStatusBackgroundColor(KycStatus);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    ///     将 ISO8601 时间字符串格式化为本地化的日期时间
    /// </summary>
    private static string FormatDateTime(string isoDateTime)
    {
        if (string.IsNullOrEmpty(isoDateTime))
            return "未知";

        if (DateTime.TryParse(isoDateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

        return isoDateTime;
    }

    /// <summary>
    ///     格式化带宽限制显示
    /// </summary>
    private static string FormatBandwidth(int bandwidthLimit)
    {
        if (bandwidthLimit <= 0) return "无限制";
        return $"{bandwidthLimit*8} Mbps";
    }

    /// <summary>
    ///     将 KYC 状态代码转换为人类可读的文本
    /// </summary>
    private static string FormatKycStatus(string status)
    {
        return status switch
        {
            "init" => "待认证",
            "certifying" => "认证中",
            "success" => "已认证",
            "failed" => "认证失败",
            _ => "未知"
        };
    }

    /// <summary>
    ///     获取 KYC 状态对应的文本颜色资源名称
    /// </summary>
    private static string GetKycStatusColor(string status)
    {
        return status switch
        {
            "init" => "SystemFillColorCautionBrush",
            "certifying" => "SystemFillColorAttentionBrush",
            "success" => "SystemFillColorSuccessBrush",
            "failed" => "SystemFillColorCriticalBrush",
            _ => "SystemFillColorNeutralBrush"
        };
    }

    /// <summary>
    ///     获取 KYC 状态对应的背景色资源名称
    /// </summary>
    private static string GetKycStatusBackgroundColor(string status)
    {
        return status switch
        {
            "init" => "SystemFillColorCautionBackgroundBrush",
            "certifying" => "SystemFillColorAttentionBackgroundBrush",
            "success" => "SystemFillColorSuccessBackgroundBrush",
            "failed" => "SystemFillColorCriticalBackgroundBrush",
            _ => "SystemFillColorNeutralBackgroundBrush"
        };
    }

    /// <summary>
    ///     将权限代码转换为人类可读的文本
    /// </summary>
    private static string FormatRole(string role)
    {
        return role switch
        {
            "user" => "普通用户",
            "admin" => "管理员",
            _ => "未知"
        };
    }
}
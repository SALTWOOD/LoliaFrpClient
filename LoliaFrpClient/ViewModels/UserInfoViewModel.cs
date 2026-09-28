using System.Collections.Generic;

namespace LoliaFrpClient.ViewModels;

/// <summary>总览页。当前只有静态占位数据,尚未接入 <c>User</c> / <c>Traffic</c>。</summary>
public sealed class UserInfoViewModel : ViewModelBase
{
    /// <summary>占位:登录后应为 User.Username。</summary>
    public string UserName { get; } = "未登录";

    /// <summary>占位:登录后应为 User.Email;未登录时提示去设置页。</summary>
    public string Email { get; } = "尚未登录,请前往「设置」使用 OAuth 登录";

    /// <summary>头像占位字符。真实头像接好后改为图片。</summary>
    public string AvatarInitial { get; } = "L";

    /// <summary>占位:顶部三个统计卡。</summary>
    public IReadOnlyList<MetricCard> Metrics { get; } =
    [
        new("已用流量", "—", "本月累计"),
        new("隧道数量", "—", "含已离线"),
        new("今日流量", "—", "较昨日 —")
    ];

    /// <summary>占位:每日流量趋势。接 <c>Traffic.GetDailyAsync()</c> 后替换。</summary>
    public IReadOnlyList<DailyTrafficPoint> DailyTraffic { get; } =
    [
        new("周一", 4.2), new("周二", 7.8), new("周三", 3.1), new("周四", 9.6),
        new("周五", 12.4), new("周六", 6.7), new("周日", 8.9)
    ];
}

/// <summary>统计卡数据。数值当前为占位。</summary>
public sealed record MetricCard(string Label, string Value, string Caption);

/// <summary>每日流量数据点。单位 GB。</summary>
public sealed record DailyTrafficPoint(string Label, double Gigabytes)
{
    /// <summary>
    ///     占位用的柱高(按 10px/GB 换算)。仅供当前演示布局,
    ///     接入真实数据后应改为由图表控件或转换器按可用高度换算。
    /// </summary>
    public double BarHeight => Gigabytes * 10;
}

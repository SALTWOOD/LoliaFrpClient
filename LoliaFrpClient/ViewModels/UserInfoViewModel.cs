using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoliaFrpClient.Api.User.Traffic.Daily;
using LoliaFrpClient.Core;

namespace LoliaFrpClient.ViewModels;

/// <summary>
///     总览页。用户卡片来自 <c>User.MeAsync()</c>,统计卡来自 <c>Traffic.GetStatsAsync()</c>,
///     趋势图来自 <c>Traffic.GetDailyAsync()</c>,隧道数量来自 <c>Tunnel.ListAsync()</c>。
/// </summary>
public sealed partial class UserInfoViewModel : ViewModelBase
{
    /// <summary>柱状图的可用高度(像素)。</summary>
    private const double MaxBarHeight = 100;

    private const string SignedOutHint = "尚未登录,请前往「设置」使用 OAuth 登录";

    /// <summary>顶部三个统计卡。</summary>
    public ObservableCollection<MetricCard> Metrics { get; } = [];

    /// <summary>每日流量趋势。</summary>
    public ObservableCollection<DailyTrafficPoint> DailyTraffic { get; } = [];

    [ObservableProperty]
    public partial string UserName { get; set; } = "未登录";

    [ObservableProperty]
    public partial string Email { get; set; } = SignedOutHint;

    /// <summary>头像占位字符。真实头像接好后改为图片。</summary>
    [ObservableProperty]
    public partial string AvatarInitial { get; set; } = "L";

    [ObservableProperty]
    public partial bool IsSignedIn { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>操作结果或取数失败的提示。为空时不显示。</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>趋势图是否有数据。没有时显示占位文案而不是一个空标题。</summary>
    [ObservableProperty]
    public partial bool HasDailyTraffic { get; set; }

    /// <summary>当前是否可签到。冷却期内服务端会返回 400,不如直接禁用按钮。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckInCommand))]
    public partial bool CanCheckIn { get; set; }

    /// <summary>签到按钮文案。冷却期内改成「今日已签到」。</summary>
    [ObservableProperty]
    public partial string CheckInLabel { get; set; } = "每日签到";

    /// <inheritdoc />
    public override Task ActivateAsync() => LoadAsync();

    /// <summary>签到。<c>CheckInAsync</c> 成功后流量额度会变,因此重新取一遍数据。</summary>
    [RelayCommand(CanExecute = nameof(CanCheckIn))]
    private async Task CheckInAsync()
    {
        IsLoading = true;

        string message;
        try
        {
            var result = await new User().CheckInAsync();
            message = result.IsSuccess
                ? $"签到成功,获得 {result.Data?.TrafficGb ?? 0} GB 流量。"
                : result.Msg;
        }
        catch (Exception ex)
        {
            message = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }

        // LoadAsync 会先清空提示,所以结果消息放在它之后写。
        await LoadAsync();
        StatusMessage = message;
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            IsSignedIn = ApiSession.Current.IsAuthenticated;

            if (!IsSignedIn)
            {
                ShowSignedOut();
                return;
            }

            await LoadProfileAsync();
            await LoadMetricsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadProfileAsync()
    {
        var me = await User.MeAsync();

        if (me is not { IsSuccess: true, Data: { } user })
        {
            StatusMessage = me.Msg;
            return;
        }

        UserName = user.Username ?? "已登录";
        Email = user.Email ?? "—";
        AvatarInitial = InitialOf(user.Username ?? user.Email);

        var cooling = user.TodayChecked == true;
        CanCheckIn = !cooling;
        CheckInLabel = cooling ? "今日已签到" : "每日签到";
    }

    private async Task LoadMetricsAsync()
    {
        var statsTask = Traffic.GetStatsAsync();
        var dailyTask = Traffic.GetDailyAsync();
        var tunnelsTask = Tunnel.ListAsync();
        await Task.WhenAll(statsTask, dailyTask, tunnelsTask);

        var stats = await statsTask;
        var daily = await dailyTask;
        var tunnels = await tunnelsTask;

        var points = daily is { IsSuccess: true, Data: { } dailyData }
            ? (dailyData.DailyStats ?? []).Where(point => point is not null).ToList()
            : [];

        var today = points.Count > 0 ? points[^1].TotalTraffic : null;
        var yesterday = points.Count > 1 ? points[^2].TotalTraffic : null;

        Metrics.Clear();
        Metrics.Add(new MetricCard("已用流量", ByteSize.Format(stats.Data?.TrafficUsed), "账户累计"));
        Metrics.Add(new MetricCard(
            "隧道数量",
            tunnels is { IsSuccess: true, Data: { } list } ? list.Count.ToString(CultureInfo.InvariantCulture) : "—",
            "含已离线"));
        Metrics.Add(new MetricCard("今日流量", ByteSize.Format(today), TrendCaption(today, yesterday)));

        BuildDailyChart(points);

        // 逐个报出失败原因:三个接口各自可能失败,只显示第一个会让人以为其余的也没数据。
        var failures = new List<string>(3);
        if (!stats.IsSuccess)
        {
            failures.Add($"流量统计:{stats.Msg}");
        }

        if (!daily.IsSuccess)
        {
            failures.Add($"每日趋势:{daily.Msg}");
        }

        if (!tunnels.IsSuccess)
        {
            failures.Add($"隧道列表:{tunnels.Msg}");
        }

        if (failures.Count > 0)
        {
            StatusMessage = string.Join(";", failures);
        }
    }

    /// <summary>把每日流量画成柱状图。柱高按区间最大值归一化,否则数值差异看不出来。</summary>
    private void BuildDailyChart(IReadOnlyList<DailyGetResponse_data_daily_stats> points)
    {
        DailyTraffic.Clear();

        var max = points.Count == 0 ? 0 : points.Max(point => point.TotalTraffic ?? 0);

        foreach (var point in points)
        {
            var bytes = point.TotalTraffic ?? 0;

            // 有流量就给个最小高度:否则「极少」和「没有」在图上都是零高,分不出来。
            var height = max > 0 && bytes > 0 ? Math.Max(2, bytes / (double)max * MaxBarHeight) : 0;

            DailyTraffic.Add(new DailyTrafficPoint(DateLabel(point.Date), ByteSize.Format(bytes), height));
        }

        HasDailyTraffic = DailyTraffic.Count > 0;
    }

    private void ShowSignedOut()
    {
        UserName = "未登录";
        Email = SignedOutHint;
        AvatarInitial = "L";
        CanCheckIn = false;
        CheckInLabel = "每日签到";

        Metrics.Clear();
        Metrics.Add(new MetricCard("已用流量", "—", "账户累计"));
        Metrics.Add(new MetricCard("隧道数量", "—", "含已离线"));
        Metrics.Add(new MetricCard("今日流量", "—", "较昨日 —"));

        DailyTraffic.Clear();
        HasDailyTraffic = false;
    }

    private static string TrendCaption(int? today, int? yesterday)
    {
        if (today is null)
        {
            return "暂无数据";
        }

        if (yesterday is null or 0)
        {
            return "较昨日 —";
        }

        var delta = (today.Value - yesterday.Value) / (double)yesterday.Value * 100;
        return delta >= 0
            ? $"较昨日 +{delta:0.#}%"
            : $"较昨日 {delta:0.#}%";
    }

    /// <summary>把 RFC3339 日期压成 <c>MM-dd</c>。解析不了就原样显示。</summary>
    private static string DateLabel(string? date) =>
        DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("MM-dd", CultureInfo.InvariantCulture)
            : date ?? "—";

    private static string InitialOf(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) ? "L" : char.ToUpperInvariant(trimmed[0]).ToString();
    }
}

/// <summary>统计卡数据。</summary>
/// <param name="Label">指标名。</param>
/// <param name="Value">主数值,已格式化。</param>
/// <param name="Caption">补充说明。</param>
public sealed record MetricCard(string Label, string Value, string Caption);

/// <summary>每日流量数据点。</summary>
/// <param name="Label">横轴标签,形如 <c>09-28</c>。</param>
/// <param name="Amount">柱顶数值,已格式化为 B/KB/MB/GB。</param>
/// <param name="BarHeight">柱高(像素),已按该区间最大值归一化。</param>
public sealed record DailyTrafficPoint(string Label, string Amount, double BarHeight);

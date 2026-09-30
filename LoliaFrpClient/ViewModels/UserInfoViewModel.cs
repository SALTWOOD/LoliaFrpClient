using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoliaFrpClient.Api.User.Traffic.Daily;
using LoliaFrpClient.Core;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.ViewModels;

// Overview page. Profile from User.MeAsync, metrics from Traffic.GetStatsAsync,
// chart from Traffic.GetDailyAsync, tunnel count from Tunnel.ListAsync.
public sealed partial class UserInfoViewModel : ViewModelBase, IDisposable
{
    // Bar chart height, in pixels.
    private const double MaxBarHeight = 100;

    private const string SignedOutHint = "尚未登录,请前往「设置」使用 OAuth 登录";

    // The three metric cards at the top.
    public ObservableCollection<MetricCard> Metrics { get; } = [];

    // Daily traffic trend.
    public ObservableCollection<DailyTrafficPoint> DailyTraffic { get; } = [];

    private readonly AvatarLoader _avatarLoader = new();

    [ObservableProperty] public partial string UserName { get; set; } = "未登录";

    [ObservableProperty] public partial string Email { get; set; } = SignedOutHint;

    // Fallback initial, shown when the avatar cannot be loaded.
    [ObservableProperty] public partial string AvatarInitial { get; set; } = "L";

    // Null when unavailable; the view falls back to AvatarInitial.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    public partial Bitmap? AvatarImage { get; set; }

    public bool HasAvatar => AvatarImage is not null;

    // Metric card columns; one column on narrow screens.
    // IsCompact is set once at startup, so no change notification is needed.
    public int MetricColumns => IsCompact ? 1 : 3;

    [ObservableProperty] public partial bool IsSignedIn { get; set; }

    [ObservableProperty] public partial bool IsLoading { get; set; }

    // Result or fetch-failure message. Hidden when null.
    [ObservableProperty] public partial string? StatusMessage { get; set; }

    // Shows placeholder text instead of an empty chart title.
    [ObservableProperty] public partial bool HasDailyTraffic { get; set; }

    // The server answers 400 during cooldown, so disable instead.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckInCommand))]
    public partial bool CanCheckIn { get; set; }

    [ObservableProperty] public partial string CheckInLabel { get; set; } = "每日签到";

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        return LoadAsync();
    }

    // Reloads afterwards: a successful check-in changes the quota.
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

        // Written after LoadAsync, which clears the message first.
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
        AvatarImage = await _avatarLoader.LoadAsync(ResolveAvatarUrl(user)).ConfigureAwait(true);

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

        // Available = limit - used, rather than the response's traffic_remaining:
        // the spec says that field currently just mirrors traffic_limit.
        var used = stats.Data?.TrafficUsed;
        var limit = stats.Data?.TrafficLimit;
        var available = used is { } usedBytes && limit is { } limitBytes
            ? Math.Max(0, limitBytes - usedBytes)
            : (long?)null;

        Metrics.Clear();
        Metrics.Add(new MetricCard(
            "可用流量",
            ByteSize.Format(available, 3),
            limit is null ? string.Empty : $"共 {ByteSize.Format(limit, 3)}"));
        Metrics.Add(new MetricCard(
            "隧道数量",
            tunnels is { IsSuccess: true, Data: { } list } ? list.Count.ToString(CultureInfo.InvariantCulture) : "—",
            string.Empty));
        Metrics.Add(new MetricCard("今日流量", ByteSize.Format(today, 3), string.Empty));

        BuildDailyChart(points);

        // Report every failure; showing only the first hides the other two.
        var failures = new List<string>(3);
        if (!stats.IsSuccess) failures.Add($"流量统计:{stats.Msg}");

        if (!daily.IsSuccess) failures.Add($"每日趋势:{daily.Msg}");

        if (!tunnels.IsSuccess) failures.Add($"隧道列表:{tunnels.Msg}");

        if (failures.Count > 0) StatusMessage = string.Join(";", failures);
    }

    // Bars are normalized to the range maximum, else differences vanish.
    private void BuildDailyChart(IReadOnlyList<DailyGetResponse_data_daily_stats> points)
    {
        DailyTraffic.Clear();

        var max = points.Count == 0 ? 0 : points.Max(point => point.TotalTraffic ?? 0);

        foreach (var point in points)
        {
            var bytes = point.TotalTraffic ?? 0;

            // A tiny non-zero bar, so "a little" is not drawn the same as "none".
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
        AvatarImage = null;
        CanCheckIn = false;
        CheckInLabel = "每日签到";

        Metrics.Clear();
        Metrics.Add(new MetricCard("可用流量", "—", string.Empty));
        Metrics.Add(new MetricCard("隧道数量", "—", "含已离线"));
        Metrics.Add(new MetricCard("今日流量", "—", string.Empty));

        DailyTraffic.Clear();
        HasDailyTraffic = false;
    }

    // RFC3339 to MM-dd; unparseable input is shown as-is.
    private static string DateLabel(string? date)
    {
        return DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("MM-dd", CultureInfo.InvariantCulture)
            : date ?? "—";
    }

    private static string InitialOf(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) ? "L" : char.ToUpperInvariant(trimmed[0]).ToString();
    }

    // Usually present: the server substitutes a Gravatar URL when the user has not set one.
    private static string? ResolveAvatarUrl(User user)
    {
        return string.IsNullOrWhiteSpace(user.Avatar) ? WeAvatarUrl(user.Email) : user.Avatar;
    }

    // weavatar mirrors Gravatar's scheme: MD5 of the trimmed, lowercased email.
    private static string? WeAvatarUrl(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized)) return null;

        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return $"https://weavatar.com/avatar/{hash}";
    }

    public void Dispose()
    {
        _avatarLoader.Dispose();
    }
}

// One metric card: label, formatted value, optional caption.
public sealed record MetricCard(string Label, string Value, string Caption);

// One bar on the daily traffic chart.
public sealed record DailyTrafficPoint(string Label, string Amount, double BarHeight);
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LoliaFrpClient.Models;

namespace LoliaFrpClient.Services;

public sealed record UserDashboardData(
    UserInfoViewModel User,
    IReadOnlyList<DailyTrafficViewModel> DailyTraffics,
    IReadOnlyList<TunnelTrafficViewModel> TunnelTraffics);

public sealed class UserInfoService
{
    private readonly ApiClientProvider _apiClientProvider = ApiClientProvider.Instance;

    public async Task<UserDashboardData> GetDashboardDataAsync()
    {
        var userTask = _apiClientProvider.Client.User.Info.GetAsInfoGetResponseAsync();
        var dailyTask = _apiClientProvider.Client.User.Traffic.Daily.GetAsDailyGetResponseAsync(config =>
            config.QueryParameters.Days = "7");
        var tunnelsTask = _apiClientProvider.Client.User.Traffic.Tunnels.GetAsTunnelsGetResponseAsync();

        await Task.WhenAll(userTask, dailyTask, tunnelsTask);

        return new UserDashboardData(
            MapUser(userTask.Result?.Data),
            MapDailyTraffics(dailyTask.Result?.Data?.DailyStats),
            MapTunnelTraffics(tunnelsTask.Result?.Data?.Tunnels));
    }

    private static UserInfoViewModel MapUser(global::LoliaFrpClient.Core.User.Info.InfoGetResponse_data? data)
    {
        return new UserInfoViewModel
        {
            Id = data?.Id ?? 0,
            Username = data?.Username ?? string.Empty,
            Email = data?.Email ?? string.Empty,
            Avatar = data?.Avatar ?? string.Empty,
            Role = data?.Role ?? string.Empty,
            KycStatus = data?.KycStatus ?? string.Empty,
            CreatedAt = data?.CreatedAt ?? string.Empty,
            MaxTunnelCount = data?.MaxTunnelCount ?? 0,
            TrafficLimit = data?.TrafficLimit ?? 0,
            TrafficUsed = data?.TrafficUsed ?? 0,
            BandwidthLimit = data?.BandwidthLimit ?? 0,
            HasKyc = data?.HasKyc ?? false,
            IsBaned = data?.IsBaned ?? false,
            TodayChecked = data?.TodayChecked ?? false
        };
    }

    private static IReadOnlyList<DailyTrafficViewModel> MapDailyTraffics(IEnumerable<global::LoliaFrpClient.Core.User.Traffic.Daily.DailyGetResponse_data_daily_stats>? dailyStats)
    {
        if (dailyStats == null)
        {
            return [];
        }

        return dailyStats.Select(item => new DailyTrafficViewModel
        {
            Date = item.Date ?? string.Empty,
            InboundBytes = item.TotalIn ?? 0,
            OutboundBytes = item.TotalOut ?? 0
        }).ToList();
    }

    private static IReadOnlyList<TunnelTrafficViewModel> MapTunnelTraffics(IEnumerable<global::LoliaFrpClient.Core.User.Traffic.Tunnels.TunnelsGetResponse_data_tunnels>? tunnelTraffics)
    {
        if (tunnelTraffics == null)
        {
            return [];
        }

        return tunnelTraffics.Select(traffic => new TunnelTrafficViewModel
        {
            TunnelName = traffic.TunnelName ?? string.Empty,
            TunnelRemark = traffic.Remark ?? "<已删除>",
            InboundBytes = traffic.TotalIn ?? 0,
            OutboundBytes = traffic.TotalOut ?? 0
        }).ToList();
    }
}

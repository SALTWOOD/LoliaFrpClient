using LoliaFrpClient.Api.User.Traffic.Charges;
using LoliaFrpClient.Api.User.Traffic.Daily;
using LoliaFrpClient.Api.User.Traffic.Stats;
using LoliaFrpClient.Api.User.Traffic.Summary;
using LoliaFrpClient.Api.User.Traffic.Tunnels;
// 本项目的 Tunnel 类会遮挡同名命名空间,故取别名。
using TrafficTunnel = LoliaFrpClient.Api.User.Traffic.Tunnel;

namespace LoliaFrpClient.Core;

public static class Traffic
{
    public static Task<ApiResult<StatsGetResponse_data>> GetStatsAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<StatsGetResponse, StatsGetResponse_data>(
            c => api.Client.User.Traffic.Stats.GetAsStatsGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<DailyGetResponse_data>> GetDailyAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<DailyGetResponse, DailyGetResponse_data>(
            c => api.Client.User.Traffic.Daily.GetAsDailyGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<SummaryGetResponse_data>> GetSummaryAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<SummaryGetResponse, SummaryGetResponse_data>(
            c => api.Client.User.Traffic.Summary.GetAsSummaryGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<ChargesGetResponse_data>> GetChargesAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<ChargesGetResponse, ChargesGetResponse_data>(
            c => api.Client.User.Traffic.Charges.GetAsChargesGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<TunnelsGetResponse_data>> GetTunnelRankingAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<TunnelsGetResponse, TunnelsGetResponse_data>(
            c => api.Client.User.Traffic.Tunnels.GetAsTunnelsGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<TrafficTunnel.Item.Tunnel_GetResponse_data>> GetTunnelAsync(
        string tunnelId,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelId);
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<TrafficTunnel.Item.Tunnel_GetResponse, TrafficTunnel.Item.Tunnel_GetResponse_data>(
            c => api.Client.User.Traffic.Tunnel[tunnelId].GetAsTunnel_GetResponseAsync(cancellationToken: c),
            r => (r.Status, r.Msg, r.Data),
            cancellationToken);
    }
}
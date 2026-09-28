using LoliaFrpClient.Api.User.Traffic.Charges;
using LoliaFrpClient.Api.User.Traffic.Daily;
using LoliaFrpClient.Api.User.Traffic.Stats;
using LoliaFrpClient.Api.User.Traffic.Summary;
using LoliaFrpClient.Api.User.Traffic.Tunnels;
// 本项目的 Tunnel 类会遮挡同名命名空间,故取别名。
using TrafficTunnel = LoliaFrpClient.Api.User.Traffic.Tunnel;

namespace LoliaFrpClient.Core;

/// <summary>
///     流量统计。全部是当前用户的只读数据,没有独立身份,故只提供静态方法。
/// </summary>
public static class Traffic
{
    /// <summary>取流量统计总览。</summary>
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

    /// <summary>取每日流量趋势。</summary>
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

    /// <summary>取流量汇总。</summary>
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

    /// <summary>取流量扣费明细。</summary>
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

    /// <summary>取各隧道流量排行。</summary>
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

    /// <summary>
    ///     按隧道 ID 取实时流量。
    /// </summary>
    /// <param name="tunnelId">隧道 ID(非隧道名)。</param>
    /// <param name="session">目标会话。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <remarks>
    ///     按隧道名查询的 <c>/user/traffic/tunnel/{tunnel_name}</c> 未包含在 SDK 中:
    ///     openapi.json 里它与本端点归一化后路径签名相同,kiota 只保留了 ID 版本。
    /// </remarks>
    public static Task<ApiResult<TrafficTunnel.Item.Tunnel_GetResponse_data>> GetTunnelAsync(
        string tunnelId,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelId);
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<TrafficTunnel.Item.Tunnel_GetResponse, TrafficTunnel.Item.Tunnel_GetResponse_data>(
            c => api.Client.User.Traffic.Tunnel[tunnelId].GetAsTunnel_GetResponseAsync(cancellationToken: c),
            // 该端点用 status 而非 code 承载业务码(spec 里确为同义字段)。
            r => (r.Status, r.Msg, r.Data),
            cancellationToken);
    }
}

using LoliaFrpClient.Api.User.Nodes;

namespace LoliaFrpClient.Core;

// Static rather than an entity: nodes have an id but expose no per-node operations.
public static class Node
{
    private const int MaxLimit = 1000;

    public static async Task<ApiResult<IReadOnlyList<NodeInfo>>> ListAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        var result = await ApiCall.RunAsync<NodesPostResponse, NodesPostResponse_data>(
            c => api.Client.User.Nodes.PostAsNodesPostResponseAsync(
                new NodesPostRequestBody { Page = 1, Limit = MaxLimit },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<NodeInfo>? nodes = result.IsSuccess
            ? [.. (result.Data?.Nodes ?? []).Where(n => n.Id is > 0).Select(From)]
            : null;

        return result.With(nodes);
    }

    private static NodeInfo From(NodesPostResponse_data_nodes node)
    {
        return new NodeInfo
        {
            Id = node.Id ?? 0,
            Name = node.Name ?? string.Empty,
            RegionCode = node.RegionCode,
            Status = node.Status ?? string.Empty,
            SupportedProtocols = node.SupportedProtocols is { Count: > 0 } protocols ? [.. protocols] : [],
            NeedKyc = node.NeedKyc ?? false,
            BeianRequired = node.BeianRequired ?? false,
            FrpsVersion = node.FrpsVersion,
            AgentVersion = node.AgentVersion,
            Sponsor = node.Sponsor,
            Bandwidth = node.Bandwidth,
            HighTraffic = node.HighTraffic ?? false,
            TrafficRatio = node.TrafficRatio,
            Remark = node.Remark,
            Load = node.Load
        };
    }
}
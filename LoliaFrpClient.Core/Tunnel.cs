using LoliaFrpClient.Api.User.Tunnel;
using LoliaFrpClient.Api.User.Tunnel.Item;
using LoliaFrpClient.Core.Frpc;

namespace LoliaFrpClient.Core;

public sealed class Tunnel : ApiFacade
{
    public Tunnel(string name) : this(name, ApiSession.Current)
    {
    }

    public Tunnel(string name, ApiSession session) : base(session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }

    public TunnelGetResponse_data_list? Summary { get; private init; }

    public static async Task<ApiResult<Tunnel>> GetAsync(
        string name,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var api = session ?? ApiSession.Current;

        var result = await ApiCall.RunAsync<WithTunnel_nameGetResponse, WithTunnel_nameGetResponse_data>(
            c => api.Client.User.Tunnel[name].GetAsWithTunnel_nameGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        return result.With(result.IsSuccess ? new Tunnel(result.Data?.Name ?? name, api) : null);
    }

    public static async Task<ApiResult<IReadOnlyList<Tunnel>>> ListAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        var result = await ApiCall.RunAsync<TunnelGetResponse, TunnelGetResponse_data>(
            c => api.Client.User.Tunnel.GetAsTunnelGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Tunnel>? tunnels = result.IsSuccess
            ?
            [
                .. (result.Data?.List ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .Select(item => new Tunnel(item.Name!, api) { Summary = item })
            ]
            : null;

        return result.With(tunnels);
    }

    public Task<ApiResult<WithTunnel_namePutResponse_data>> UpdateAsync(
        WithTunnel_namePutRequestBody changes,
        CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<WithTunnel_namePutResponse, WithTunnel_namePutResponse_data>(
            c => Client.User.Tunnel[Name].PutAsWithTunnel_namePutResponseAsync(changes, cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public Task<ApiResult> DeleteAsync(CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<WithTunnel_nameDeleteResponse>(
            c => Client.User.Tunnel[Name].DeleteAsWithTunnel_nameDeleteResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }

    public Task<ApiResult<WithTunnel_nameGetResponse_data>> GetDetailAsync(
        CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<WithTunnel_nameGetResponse, WithTunnel_nameGetResponse_data>(
            c => Client.User.Tunnel[Name].GetAsWithTunnel_nameGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    // Mapped out of the generated DTO so callers never have to reference the Api project.
    public async Task<ApiResult<TunnelDetail>> GetDetailInfoAsync(CancellationToken cancellationToken = default)
    {
        var detail = await GetDetailAsync(cancellationToken).ConfigureAwait(false);
        if (!detail.IsSuccess || detail.Data is not { } data) return detail.With<TunnelDetail>(null);

        return detail.With(new TunnelDetail
        {
            Id = data.Id ?? 0,
            Name = data.Name ?? Name,
            Remark = data.Remark,
            Type = data.Type,
            NodeName = data.NodeName,
            NodeAddress = data.NodeAddress,
            LocalIp = data.LocalIp,
            LocalPort = data.LocalPort,
            RemotePort = data.RemotePort,
            CustomDomain = data.CustomDomain,
            Status = data.Status,
            CreatedAt = data.CreatedAt,
            BandwidthLimit = data.BandwidthLimit,
            ClientVersion = data.ClientVersion,
            Token = data.TunnelToken
        });
    }

    public static async Task<ApiResult<Tunnel>> CreateAsync(
        CreateTunnelSpec spec,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        var result = await ApiCall.RunAsync<TunnelPostResponse, TunnelPostResponse_data>(
            c => api.Client.User.Tunnel.PostAsTunnelPostResponseAsync(
                new TunnelPostRequestBody
                {
                    NodeId = spec.NodeId,
                    Type = ParseType(spec.Type),
                    LocalIp = spec.LocalIp,
                    LocalPort = spec.LocalPort,
                    RemotePort = spec.RemotePort,
                    CustomDomain = spec.CustomDomain,
                    Remark = spec.Remark
                },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        // A returned name is proof the tunnel exists, so creation is not reported as failed
        // purely because the envelope's code was unexpected.
        var name = result.Data?.Name;
        if (string.IsNullOrWhiteSpace(name)) return result.With<Tunnel>(null);

        var tunnel = new Tunnel(name, api);

        return result.IsSuccess
            ? result.With(tunnel)
            : new ApiResult<Tunnel> { IsSuccess = true, Code = result.Code, Msg = result.Msg, Data = tunnel };
    }

    // The generated body takes an enum; the facade keeps strings so callers stay DTO-free.
    private static TunnelPostRequestBody_type ParseType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "udp" => TunnelPostRequestBody_type.Udp,
            "http" => TunnelPostRequestBody_type.Http,
            "https" => TunnelPostRequestBody_type.Https,
            _ => TunnelPostRequestBody_type.Tcp
        };
    }

    public Task<ApiResult> UpdateRemarkAsync(string? remark, CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<WithTunnel_namePutResponse>(
            c => Client.User.Tunnel[Name].PutAsWithTunnel_namePutResponseAsync(
                new WithTunnel_namePutRequestBody { Remark = remark },
                cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }

    public async Task<ApiResult<FrpcLaunchCredential>> GetLaunchCredentialAsync(
        CancellationToken cancellationToken = default)
    {
        var detail = await GetDetailAsync(cancellationToken).ConfigureAwait(false);
        if (!detail.IsSuccess) return detail.With<FrpcLaunchCredential>(null);

        var token = detail.Data?.TunnelToken;
        if (string.IsNullOrWhiteSpace(token))
            return new ApiResult<FrpcLaunchCredential>
            {
                Code = detail.Code,
                Msg = "服务端未返回隧道 token",
                Failure = ApiFailureKind.Server
            };

        return detail.With(new FrpcLaunchCredential
        {
            TunnelId = detail.Data?.Id ?? 0,
            Token = token
        });
    }
}
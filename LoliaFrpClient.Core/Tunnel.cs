using LoliaFrpClient.Api.User.Tunnel;
using LoliaFrpClient.Api.User.Tunnel.Item;

namespace LoliaFrpClient.Core;

/// <summary>
///     隧道实体。由 <see cref="User" /> 的创建/列表方法产出,也可用 <see cref="GetAsync" /> 直接取。
///     <para>身份是隧道名——服务端用 <c>/user/tunnel/{tunnel_name}</c> 作为路径,所以名字即主键。</para>
/// </summary>
public sealed class Tunnel : ApiFacade
{
    /// <summary>用当前会话创建一个指向指定隧道的引用。</summary>
    /// <param name="name">隧道名称。</param>
    public Tunnel(string name) : this(name, ApiSession.Current)
    {
    }

    /// <summary>用指定会话创建一个指向指定隧道的引用。</summary>
    public Tunnel(string name, ApiSession session) : base(session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>隧道名称,同时是路径标识。</summary>
    public string Name { get; }

    /// <summary>按名称取隧道。</summary>
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

    /// <summary>取当前用户的隧道列表。</summary>
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
            ? [.. (result.Data?.List ?? [])
                .Select(item => item.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => new Tunnel(name!, api))]
            : null;

        return result.With(tunnels);
    }

    /// <summary>修改本隧道。</summary>
    public Task<ApiResult<WithTunnel_namePutResponse_data>> UpdateAsync(
        WithTunnel_namePutRequestBody changes,
        CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<WithTunnel_namePutResponse, WithTunnel_namePutResponse_data>(
            c => Client.User.Tunnel[Name].PutAsWithTunnel_namePutResponseAsync(changes, cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);

    /// <summary>删除本隧道。</summary>
    public Task<ApiResult> DeleteAsync(CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<WithTunnel_nameDeleteResponse>(
            c => Client.User.Tunnel[Name].DeleteAsWithTunnel_nameDeleteResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);

    /// <summary>取本隧道的详情快照。</summary>
    public Task<ApiResult<WithTunnel_nameGetResponse_data>> GetDetailAsync(CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<WithTunnel_nameGetResponse, WithTunnel_nameGetResponse_data>(
            c => Client.User.Tunnel[Name].GetAsWithTunnel_nameGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
}

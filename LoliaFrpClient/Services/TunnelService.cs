using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LoliaFrpClient.Core.User.Tunnel;
using LoliaFrpClient.Models;

namespace LoliaFrpClient.Services;

public sealed class TunnelService
{
    private readonly ApiClientProvider _apiClientProvider = ApiClientProvider.Instance;
    private readonly FrpcManager _frpcManager = ServiceLocator.FrpcManager;

    public async Task<IReadOnlyList<TunnelViewModel>> GetTunnelsAsync()
    {
        var response = await _apiClientProvider.Client.User.Tunnel.GetAsTunnelGetResponseAsync();
        var tunnelList = response?.Data?.List;

        if (tunnelList == null)
        {
            return [];
        }

        return tunnelList.Select(tunnel =>
        {
            var tunnelId = tunnel.Id ?? 0;

            return new TunnelViewModel
            {
                Id = tunnelId,
                Name = tunnel.Name ?? string.Empty,
                Type = tunnel.Type ?? string.Empty,
                Status = tunnel.Status ?? string.Empty,
                Remark = tunnel.Remark ?? string.Empty,
                CustomDomain = tunnel.CustomDomain ?? string.Empty,
                LocalIp = tunnel.LocalIp ?? string.Empty,
                LocalPort = tunnel.LocalPort ?? 0,
                RemotePort = tunnel.RemotePort ?? 0,
                NodeId = tunnel.NodeId ?? 0,
                BandwidthLimit = tunnel.BandwidthLimit ?? 0,
                IsEnabled = _frpcManager.IsTunnelProcessRunning(tunnelId)
            };
        }).ToList();
    }

    public IEnumerable<TunnelViewModel> FilterTunnels(IEnumerable<TunnelViewModel> tunnels, string filterType, string searchText)
    {
        var query = tunnels;

        if (!string.Equals(filterType, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(tunnel => string.Equals(tunnel.Type, filterType, StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return query;
        }

        return query.Where(tunnel =>
            tunnel.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            tunnel.Remark.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            tunnel.CustomDomain.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    public async Task CreateTunnelAsync(TunnelPostRequestBody requestBody)
    {
        await _apiClientProvider.Client.User.Tunnel.PostAsync(requestBody);
    }

    public async Task DeleteTunnelAsync(TunnelViewModel tunnel)
    {
        if (tunnel.IsEnabled)
        {
            StopTunnel(tunnel);
        }

        await _apiClientProvider.Client.User.Tunnel[tunnel.Name].DeleteAsWithTunnel_nameDeleteResponseAsync();
    }

    public async Task StartTunnelAsync(TunnelViewModel tunnel)
    {
        var tokenResponse = await _apiClientProvider.Client.User.Tunnel[tunnel.Name].GetAsWithTunnel_nameGetResponseAsync();
        var id = tokenResponse?.Data?.Id;
        var token = tokenResponse?.Data?.TunnelToken;

        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("Unable to get tunnel token.");
        }

        _frpcManager.Start(tunnel.Id, tunnel.Name, $"-t {id}:{token}");
        tunnel.IsEnabled = true;
    }

    public void StopTunnel(TunnelViewModel tunnel)
    {
        _frpcManager.Stop(tunnel.Id);
        tunnel.IsEnabled = false;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LoliaFrpClient.Constants;
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

        if (!string.Equals(filterType, TunnelType.All, StringComparison.OrdinalIgnoreCase))
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

        if (string.IsNullOrEmpty(token) || id == null)
        {
            throw new TunnelTokenUnavailableException();
        }

        _frpcManager.Start(tunnel.Id, tunnel.Name, $"-t {id}:{token}");
        tunnel.IsEnabled = true;
    }

    /// <summary>
    ///     尝试启用隧道，把「是否已安装 frpc / 能否取得 token / 是否登录失效」
    ///     这些判断从页面移到服务层，页面只负责根据结果决定弹什么样的对话框。
    /// </summary>
    public async Task<TunnelActionResult> TryEnableAsync(TunnelViewModel tunnel)
    {
        if (_frpcManager.GetInstallStatus(null) == FrpcInstallStatus.NotInstalled)
        {
            return TunnelActionResult.FrpcNotInstalled;
        }

        try
        {
            await StartTunnelAsync(tunnel);
            return TunnelActionResult.Success;
        }
        catch (TunnelTokenUnavailableException)
        {
            return TunnelActionResult.TokenUnavailable;
        }
        catch (Exception ex) when (AuthErrorHelper.ShouldSilence(ex))
        {
            // 登录已失效，AuthSessionService 会统一处理，页面不再弹错
            return TunnelActionResult.Silent;
        }
        catch (Exception ex)
        {
            return TunnelActionResult.Failed(ex.Message);
        }
    }

    /// <summary>
    ///     尝试停用隧道。进程未运行时同样视为成功。
    /// </summary>
    public TunnelActionResult TryDisable(TunnelViewModel tunnel)
    {
        try
        {
            StopTunnel(tunnel);
            return TunnelActionResult.Success;
        }
        catch (Exception ex)
        {
            return TunnelActionResult.Failed(ex.Message);
        }
    }

    /// <summary>
    ///     记录停止隧道的实际效果，供页面决定后续 UI。
    /// </summary>
    public void StopTunnel(TunnelViewModel tunnel)
    {
        _frpcManager.Stop(tunnel.Id);
        tunnel.IsEnabled = false;
    }
}

/// <summary>
///     隧道操作的结果。失败时 <see cref="ErrorMessage"/> 为可直接展示的文本。
/// </summary>
public sealed record TunnelActionResult(
    TunnelActionResultKind Kind,
    string? ErrorMessage = null)
{
    public static readonly TunnelActionResult Success = new(TunnelActionResultKind.Success);
    public static readonly TunnelActionResult FrpcNotInstalled = new(TunnelActionResultKind.FrpcNotInstalled);
    public static readonly TunnelActionResult TokenUnavailable = new(TunnelActionResultKind.TokenUnavailable);
    public static readonly TunnelActionResult Silent = new(TunnelActionResultKind.Silent);

    public static TunnelActionResult Failed(string message) => new(TunnelActionResultKind.Failed, message);

    public bool IsSuccess => Kind == TunnelActionResultKind.Success;
}

public enum TunnelActionResultKind
{
    Success,

    /// <summary>尚未安装 frpc 核心。</summary>
    FrpcNotInstalled,

    /// <summary>无法从服务端取得隧道连接密钥。</summary>
    TokenUnavailable,

    /// <summary>登录已失效，已交由 AuthSessionService 处理，页面无需提示。</summary>
    Silent,

    /// <summary>其他失败，<see cref="TunnelActionResult.ErrorMessage"/> 为原因。</summary>
    Failed
}

/// <summary>
///     服务端未返回隧道连接密钥时抛出。
/// </summary>
public sealed class TunnelTokenUnavailableException : Exception
{
    public TunnelTokenUnavailableException()
        : base("Unable to get tunnel token.")
    {
    }
}

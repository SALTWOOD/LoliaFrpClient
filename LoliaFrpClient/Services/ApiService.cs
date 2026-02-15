using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using LoliaFrpClient.Models;

namespace LoliaFrpClient.Services
{
    /// <summary>
    /// API 服务类，用于调用 LoliaFrp API
    /// </summary>
    public class ApiService
    {
        private static readonly Lazy<ApiService> _instance = new Lazy<ApiService>(() => new ApiService());
        public static ApiService Instance => _instance.Value;

        private readonly HttpClient _httpClient;
        private readonly SettingsStorage _settings;
        private string _basePath;

        private ApiService()
        {
            _httpClient = new HttpClient();
            _settings = SettingsStorage.Instance;

            // 获取 API 基础路径
            _basePath = _settings.Read<string>("ApiBasePath", "https://api.lolia.link/api/v1");
        }

        /// <summary>
        /// 设置 API 基础路径
        /// </summary>
        public void SetBasePath(string basePath)
        {
            _settings.Write("ApiBasePath", basePath);
            _basePath = basePath;
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        public async Task<UserInfoViewModel?> GetUserInfoAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_basePath}/user/info");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<UserInfoData>>(json);

                if (result?.Data != null)
                {
                    return new UserInfoViewModel
                    {
                        Id = result.Data.Id,
                        Username = result.Data.Username,
                        Email = result.Data.Email,
                        Avatar = result.Data.Avatar,
                        Role = result.Data.Role,
                        KycStatus = result.Data.KycStatus,
                        CreatedAt = result.Data.CreatedAt,
                        MaxTunnelCount = result.Data.MaxTunnelCount,
                        TrafficLimit = result.Data.TrafficLimit,
                        TrafficUsed = result.Data.TrafficUsed,
                        BandwidthLimit = result.Data.BandwidthLimit,
                        HasKyc = result.Data.HasKyc,
                        IsBaned = result.Data.IsBaned,
                        TodayChecked = result.Data.TodayChecked
                    };
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"获取用户信息失败: {ex.Message}", ex);
            }

            return null;
        }

        /// <summary>
        /// 获取隧道列表
        /// </summary>
        public async Task<List<TunnelViewModel>> GetTunnelListAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_basePath}/user/tunnel");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<TunnelListData>>(json);

                if (result?.Data?.List != null)
                {
                    var tunnels = new List<TunnelViewModel>();
                    foreach (var tunnel in result.Data.List)
                    {
                        tunnels.Add(new TunnelViewModel
                        {
                            Id = tunnel.Id,
                            Name = tunnel.Name,
                            Type = tunnel.Type,
                            Status = tunnel.Status,
                            Remark = tunnel.Remark ?? string.Empty,
                            CustomDomain = tunnel.CustomDomain ?? string.Empty,
                            LocalIp = tunnel.LocalIp ?? string.Empty,
                            LocalPort = tunnel.LocalPort,
                            RemotePort = tunnel.RemotePort,
                            NodeId = tunnel.NodeId,
                            BandwidthLimit = tunnel.BandwidthLimit
                        });
                    }
                    return tunnels;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"获取隧道列表失败: {ex.Message}", ex);
            }

            return new List<TunnelViewModel>();
        }

        /// <summary>
        /// 获取隧道详情
        /// </summary>
        public async Task<TunnelViewModel?> GetTunnelDetailAsync(string tunnelName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_basePath}/user/tunnel/{tunnelName}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<TunnelDetailData>>(json);

                if (result?.Data != null)
                {
                    return new TunnelViewModel
                    {
                        Id = result.Data.Id,
                        Name = result.Data.Name,
                        Type = result.Data.Type,
                        Status = result.Data.Status,
                        Remark = result.Data.Remark ?? string.Empty,
                        CustomDomain = result.Data.CustomDomain ?? string.Empty,
                        LocalIp = result.Data.LocalIp ?? string.Empty,
                        LocalPort = result.Data.LocalPort,
                        RemotePort = result.Data.RemotePort,
                        NodeId = result.Data.NodeId,
                        BandwidthLimit = result.Data.BandwidthLimit
                    };
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"获取隧道详情失败: {ex.Message}", ex);
            }

            return null;
        }

        /// <summary>
        /// 获取流量统计
        /// </summary>
        public async Task<TrafficStatsViewModel?> GetTrafficStatsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_basePath}/user/traffic/stats");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<TrafficStatsData>>(json);

                if (result?.Data != null)
                {
                    return new TrafficStatsViewModel
                    {
                        UserId = result.Data.UserId,
                        Username = result.Data.Username,
                        TrafficLimit = result.Data.TrafficLimit,
                        TrafficUsed = result.Data.TrafficUsed,
                        TrafficRemaining = result.Data.TrafficRemaining
                    };
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"获取流量统计失败: {ex.Message}", ex);
            }

            return null;
        }

        /// <summary>
        /// 获取隧道流量信息
        /// </summary>
        public async Task<List<TunnelTrafficViewModel>> GetTunnelTrafficAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_basePath}/user/traffic/tunnels");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<TunnelTrafficListData>>(json);

                if (result?.Data?.Tunnels != null)
                {
                    var traffics = new List<TunnelTrafficViewModel>();
                    foreach (var tunnel in result.Data.Tunnels)
                    {
                        traffics.Add(new TunnelTrafficViewModel
                        {
                            TunnelId = tunnel.TunnelId,
                            TunnelName = tunnel.TunnelName,
                            InboundBytes = tunnel.InboundBytes,
                            OutboundBytes = tunnel.OutboundBytes
                        });
                    }
                    return traffics;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"获取隧道流量信息失败: {ex.Message}", ex);
            }

            return new List<TunnelTrafficViewModel>();
        }

        /// <summary>
        /// 获取节点列表
        /// </summary>
        public async Task<List<NodeInfo>> GetNodesAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_basePath}/user/nodes", null);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<NodesData>>(json);

                if (result?.Data?.Nodes != null)
                {
                    return result.Data.Nodes;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"获取节点列表失败: {ex.Message}", ex);
            }

            return new List<NodeInfo>();
        }

        /// <summary>
        /// 获取客户端最新版本
        /// </summary>
        public async Task<string?> GetClientVersionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_basePath}/client/version");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ClientVersionResponse>(json);
                return result?.Data?.Version;
            }
            catch (Exception ex)
            {
                throw new Exception($"获取客户端版本失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取 frpc 配置
        /// </summary>
        public async Task<string?> GetFrpcConfigAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_basePath}/user/frpc/config");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<FrpcConfigData>>(json);

                return result?.Data?.Config;
            }
            catch (Exception ex)
            {
                throw new Exception($"获取 frpc 配置失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 通过 token 获取 frpc 配置
        /// </summary>
        public async Task<string?> GetFrpcConfigByTokenAsync(string token)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_basePath}/tunnel/frpc/config/{token}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<FrpcConfigResponse>(json);
                return result?.Data?.Config;
            }
            catch (Exception ex)
            {
                throw new Exception($"通过 token 获取 frpc 配置失败: {ex.Message}", ex);
            }
        }

        #region 内部数据模型

        private class ClientVersionResponse
        {
            public int Code { get; set; }
            public string Msg { get; set; } = string.Empty;
            public ClientVersionData? Data { get; set; }
        }

        private class ClientVersionData
        {
            public string Version { get; set; } = string.Empty;
        }

        private class FrpcConfigResponse
        {
            public int Code { get; set; }
            public string Msg { get; set; } = string.Empty;
            public FrpcConfigData? Data { get; set; }
        }

        private class FrpcConfigData
        {
            public string Config { get; set; } = string.Empty;
        }

        private class ApiResponse<T>
        {
            public int Code { get; set; }
            public string Msg { get; set; } = string.Empty;
            public T? Data { get; set; }
        }

        private class UserInfoData
        {
            public int Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Avatar { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string KycStatus { get; set; } = string.Empty;
            public string CreatedAt { get; set; } = string.Empty;
            public int MaxTunnelCount { get; set; }
            public int TrafficLimit { get; set; }
            public int TrafficUsed { get; set; }
            public int BandwidthLimit { get; set; }
            public bool HasKyc { get; set; }
            public bool IsBaned { get; set; }
            public bool TodayChecked { get; set; }
        }

        private class TunnelListData
        {
            public List<TunnelItem> List { get; set; } = new List<TunnelItem>();
        }

        private class TunnelItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? Remark { get; set; }
            public string? CustomDomain { get; set; }
            public string? LocalIp { get; set; }
            public int LocalPort { get; set; }
            public int RemotePort { get; set; }
            public int NodeId { get; set; }
            public int BandwidthLimit { get; set; }
        }

        private class TunnelDetailData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? Remark { get; set; }
            public string? CustomDomain { get; set; }
            public string? LocalIp { get; set; }
            public int LocalPort { get; set; }
            public int RemotePort { get; set; }
            public int NodeId { get; set; }
            public int BandwidthLimit { get; set; }
        }

        private class TrafficStatsData
        {
            public string UserId { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public int TrafficLimit { get; set; }
            public int TrafficUsed { get; set; }
            public int TrafficRemaining { get; set; }
        }

        private class TunnelTrafficListData
        {
            public List<TunnelTrafficItem> Tunnels { get; set; } = new List<TunnelTrafficItem>();
        }

        private class TunnelTrafficItem
        {
            public int TunnelId { get; set; }
            public string TunnelName { get; set; } = string.Empty;
            public long InboundBytes { get; set; }
            public long OutboundBytes { get; set; }
        }

        private class NodesData
        {
            public List<NodeInfo> Nodes { get; set; } = new List<NodeInfo>();
        }

        #endregion
    }

    /// <summary>
    /// 节点信息
    /// </summary>
    public class NodeInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Bandwidth { get; set; }
        public int Online { get; set; }

        /// <summary>
        /// 状态显示文本
        /// </summary>
        public string StatusText
        {
            get
            {
                return Status switch
                {
                    "online" => "在线",
                    "offline" => "离线",
                    "maintenance" => "维护中",
                    _ => Status
                };
            }
        }

        /// <summary>
        /// 状态颜色画刷
        /// </summary>
        public Brush StatusBrush
        {
            get
            {
                return Status switch
                {
                    "online" => new SolidColorBrush(Microsoft.UI.Colors.Green),
                    "offline" => new SolidColorBrush(Microsoft.UI.Colors.Red),
                    "maintenance" => new SolidColorBrush(Microsoft.UI.Colors.Orange),
                    _ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
            }
        }
    }
}

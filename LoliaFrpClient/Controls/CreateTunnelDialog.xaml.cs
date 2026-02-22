using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LoliaFrpClient.Core.User.Tunnel;
using LoliaFrpClient.Models;
using LoliaFrpClient.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LoliaFrpClient.Controls;

/// <summary>
///     创建隧道对话框
/// </summary>
public sealed partial class CreateTunnelDialog : ContentDialog
{
    private readonly ApiClientProvider _apiClientProvider;
    private readonly List<NodeInfo> _nodes = new();

    public CreateTunnelDialog()
    {
        InitializeComponent();
        _apiClientProvider = ApiClientProvider.Instance;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadNodesAsync();
    }

    /// <summary>
    ///     加载节点列表
    /// </summary>
    private async Task LoadNodesAsync()
    {
        try
        {
            var response = await _apiClientProvider.Client.User.Nodes.PostAsNodesPostResponseAsync();
            var nodesData = response?.Data?.Nodes;

            if (nodesData != null)
            {
                _nodes.Clear();
                foreach (var node in nodesData)
                {
                    var nodeInfo = new NodeInfo
                    {
                        Id = node.Id ?? 0,
                        Name = node.Name ?? string.Empty,
                        Status = node.Status ?? string.Empty,
                        Host = node.IpAddress ?? string.Empty,
                        Port = node.FrpsPort ?? 0,
                        AgentVersion = node.AgentVersion ?? string.Empty,
                        FrpsVersion = node.FrpsVersion ?? string.Empty,
                        Sponsor = node.Sponsor ?? string.Empty,
                        LastSeen = node.LastSeen ?? string.Empty,
                        Bandwidth = node.Bandwidth ?? 0
                    };

                    // 设置支持的协议
                    if (node.SupportedProtocols != null)
                        nodeInfo.SupportedProtocols = string.Join(", ", node.SupportedProtocols);

                    // 设置显示名称
                    nodeInfo.DisplayName = $"{nodeInfo.Name} ({nodeInfo.Host}:{nodeInfo.Port})";

                    _nodes.Add(nodeInfo);
                }

                // 只显示在线的节点
                NodeComboBox.ItemsSource = _nodes.Where(n => n.Online).ToList();
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("加载节点失败", ex.Message);
        }
    }

    /// <summary>
    ///     隧道类型选择变化
    /// </summary>
    private void OnTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var type = selectedItem.Tag?.ToString();
            // HTTP/HTTPS需要显示自定义域名
            CustomDomainPanel.Visibility = type == "http" || type == "https"
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    /// <summary>
    ///     节点选择变化
    /// </summary>
    private void OnNodeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NodeComboBox.SelectedItem is NodeInfo node)
        {
            NodeInfoBorder.Visibility = Visibility.Visible;
            NodeNameTextBlock.Text = $"名称: {node.Name}";
            NodeStatusTextBlock.Text = $"状态: {node.StatusText}";
            NodeProtocolsTextBlock.Text = $"支持的协议: {node.SupportedProtocols}";
        }
        else
        {
            NodeInfoBorder.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    ///     验证输入
    /// </summary>
    private bool ValidateInput(out string errorMessage)
    {
        errorMessage = string.Empty;

        // 验证隧道类型
        if (TypeComboBox.SelectedItem == null)
        {
            errorMessage = "请选择隧道类型";
            return false;
        }

        // 验证节点
        if (NodeComboBox.SelectedItem == null)
        {
            errorMessage = "请选择节点";
            return false;
        }

        // 验证本地IP
        if (string.IsNullOrWhiteSpace(LocalIpTextBox.Text))
        {
            errorMessage = "请输入本地IP";
            return false;
        }

        // 验证本地端口
        if (LocalPortNumberBox.Value <= 0)
        {
            errorMessage = "请输入有效的本地端口";
            return false;
        }

        // 验证自定义域名（HTTP/HTTPS）
        if (TypeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var type = selectedItem.Tag?.ToString();
            if ((type == "http" || type == "https") && string.IsNullOrWhiteSpace(CustomDomainTextBox.Text))
            {
                errorMessage = "HTTP/HTTPS隧道需要输入自定义域名";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     获取创建隧道的请求体
    /// </summary>
    public TunnelPostRequestBody GetTunnelRequestBody()
    {
        if (TypeComboBox.SelectedItem is ComboBoxItem selectedItem && NodeComboBox.SelectedItem is NodeInfo node)
        {
            var type = selectedItem.Tag?.ToString();

            return new TunnelPostRequestBody
            {
                Type = type ?? string.Empty,
                NodeId = node.Id,
                LocalIp = LocalIpTextBox.Text,
                LocalPort = (int)LocalPortNumberBox.Value,
                RemotePort = RemotePortNumberBox.Value > 0 ? (int?)RemotePortNumberBox.Value : null,
                CustomDomain = CustomDomainTextBox.Text,
                Remark = RemarkTextBox.Text
            };
        }

        throw new InvalidOperationException("请填写完整的隧道信息");
    }

    /// <summary>
    ///     显示错误对话框
    /// </summary>
    private async Task ShowErrorDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "确定",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

    /// <summary>
    ///     主按钮点击事件（创建按钮）
    /// </summary>
    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 验证输入
        if (!ValidateInput(out var errorMessage))
        {
            args.Cancel = true;
            await ShowErrorDialogAsync("输入验证失败", errorMessage);
        }
    }
}
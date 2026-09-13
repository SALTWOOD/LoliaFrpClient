using System.Collections.Generic;
using LoliaFrpClient.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LoliaFrpClient.Controls;

/// <summary>
///     隧道详情弹窗的内容体。
///     <para>
///         把原本写在页面里的 UI 构造代码移到这里，行数由字段列表推导，
///         不再依赖手写的行数常量。
///     </para>
/// </summary>
public static class TunnelDetailContent
{
    /// <summary>
    ///     根据隧道构建「标签 / 值」两列表格。
    /// </summary>
    public static UIElement Create(TunnelViewModel tunnel)
    {
        var fields = new List<(string Label, string Value)>
        {
            ("名称:", tunnel.Name),
            ("类型:", tunnel.TypeDisplayText),
            ("状态:", tunnel.StatusDisplayText),
            ("备注:", tunnel.Remark),
            ("自定义域名:", tunnel.CustomDomain),
            ("本地地址:", $"{tunnel.LocalIp}:{tunnel.LocalPort}"),
            ("远程端口:", tunnel.RemotePort.ToString()),
            ("节点 ID:", tunnel.NodeId.ToString())
        };

        var infoGrid = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var i = 0; i < fields.Count; i++)
        {
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddRow(infoGrid, i, fields[i].Label, fields[i].Value);
        }

        return new StackPanel
        {
            Spacing = 12,
            Children = { infoGrid }
        };
    }

    private static void AddRow(Grid grid, int row, string label, string value)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetRow(labelBlock, row);
        Grid.SetColumn(labelBlock, 0);

        var valueBlock = new TextBlock
        {
            Text = value,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(valueBlock, row);
        Grid.SetColumn(valueBlock, 1);

        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
    }
}

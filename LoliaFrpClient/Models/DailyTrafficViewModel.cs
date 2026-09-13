using LoliaFrpClient.Services;

namespace LoliaFrpClient.Models;

/// <summary>
///     每日流量视图模型
/// </summary>
public class DailyTrafficViewModel
{
    /// <summary>
    ///     日期
    /// </summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>
    ///     入站流量（字节）
    /// </summary>
    public long InboundBytes { get; set; }

    /// <summary>
    ///     出站流量（字节）
    /// </summary>
    public long OutboundBytes { get; set; }

    /// <summary>
    ///     总流量（字节）
    /// </summary>
    public long TotalBytes => InboundBytes + OutboundBytes;

    /// <summary>
    ///     格式化的入站流量
    /// </summary>
    public string FormattedInbound => ByteFormatter.Format(InboundBytes);

    /// <summary>
    ///     格式化的出站流量
    /// </summary>
    public string FormattedOutbound => ByteFormatter.Format(OutboundBytes);

    /// <summary>
    ///     格式化的总流量
    /// </summary>
    public string FormattedTotal => ByteFormatter.Format(TotalBytes);
}

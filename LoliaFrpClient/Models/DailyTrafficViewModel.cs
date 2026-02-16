using System;

namespace LoliaFrpClient.Models
{
    /// <summary>
    /// 每日流量视图模型
    /// </summary>
    public class DailyTrafficViewModel
    {
        /// <summary>
        /// 日期
        /// </summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>
        /// 入站流量（字节）
        /// </summary>
        public long InboundBytes { get; set; }

        /// <summary>
        /// 出站流量（字节）
        /// </summary>
        public long OutboundBytes { get; set; }

        /// <summary>
        /// 总流量（字节）
        /// </summary>
        public long TotalBytes => InboundBytes + OutboundBytes;

        /// <summary>
        /// 格式化的入站流量
        /// </summary>
        public string FormattedInbound => FormatBytes(InboundBytes);

        /// <summary>
        /// 格式化的出站流量
        /// </summary>
        public string FormattedOutbound => FormatBytes(OutboundBytes);

        /// <summary>
        /// 格式化的总流量
        /// </summary>
        public string FormattedTotal => FormatBytes(TotalBytes);

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

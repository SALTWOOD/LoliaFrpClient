using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LoliaFrpClient.Models
{
    /// <summary>
    /// 隧道流量视图模型
    /// </summary>
    public class TunnelTrafficViewModel : INotifyPropertyChanged
    {
        private int _tunnelId;
        private string _tunnelName = string.Empty;
        private long _inboundBytes;
        private long _outboundBytes;
        private long _totalBytes;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int TunnelId
        {
            get => _tunnelId;
            set { _tunnelId = value; OnPropertyChanged(); }
        }

        public string TunnelName
        {
            get => _tunnelName;
            set { _tunnelName = value; OnPropertyChanged(); }
        }

        public long InboundBytes
        {
            get => _inboundBytes;
            set { _inboundBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedInbound)); OnPropertyChanged(nameof(TotalBytes)); OnPropertyChanged(nameof(FormattedTotal)); }
        }

        public long OutboundBytes
        {
            get => _outboundBytes;
            set { _outboundBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedOutbound)); OnPropertyChanged(nameof(TotalBytes)); OnPropertyChanged(nameof(FormattedTotal)); }
        }

        public long TotalBytes
        {
            get => _totalBytes = InboundBytes + OutboundBytes;
        }

        /// <summary>
        /// 格式化的入站流量
        /// </summary>
        public string FormattedInbound => TrafficStatsViewModel.FormatTraffic(InboundBytes);

        /// <summary>
        /// 格式化的出站流量
        /// </summary>
        public string FormattedOutbound => TrafficStatsViewModel.FormatTraffic(OutboundBytes);

        /// <summary>
        /// 格式化的总流量
        /// </summary>
        public string FormattedTotal => TrafficStatsViewModel.FormatTraffic(TotalBytes);
    }
}

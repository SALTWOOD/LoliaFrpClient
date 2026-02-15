using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LoliaFrpClient.Models
{
    /// <summary>
    /// 流量统计视图模型
    /// </summary>
    public class TrafficStatsViewModel : INotifyPropertyChanged
    {
        private string _userId = string.Empty;
        private string _username = string.Empty;
        private int _trafficLimit;
        private int _trafficUsed;
        private int _trafficRemaining;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string UserId
        {
            get => _userId;
            set { _userId = value; OnPropertyChanged(); }
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public int TrafficLimit
        {
            get => _trafficLimit;
            set { _trafficLimit = value; OnPropertyChanged(); }
        }

        public int TrafficUsed
        {
            get => _trafficUsed;
            set { _trafficUsed = value; OnPropertyChanged(); }
        }

        public int TrafficRemaining
        {
            get => _trafficRemaining;
            set { _trafficRemaining = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 流量使用百分比
        /// </summary>
        public double TrafficUsagePercentage => TrafficLimit > 0 ? (double)TrafficUsed / TrafficLimit * 100 : 0;

        /// <summary>
        /// 格式化的流量限制
        /// </summary>
        public string FormattedTrafficLimit => FormatTraffic(TrafficLimit);

        /// <summary>
        /// 格式化的已用流量
        /// </summary>
        public string FormattedTrafficUsed => FormatTraffic(TrafficUsed);

        /// <summary>
        /// 格式化的剩余流量
        /// </summary>
        public string FormattedTrafficRemaining => FormatTraffic(TrafficRemaining);

        /// <summary>
        /// 格式化流量大小
        /// </summary>
        public static string FormatTraffic(long bytes)
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

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LoliaFrpClient.Models
{
    /// <summary>
    /// 用户信息视图模型
    /// </summary>
    public class UserInfoViewModel : INotifyPropertyChanged
    {
        private int _id;
        private string _username = string.Empty;
        private string _email = string.Empty;
        private string _avatar = string.Empty;
        private string _role = string.Empty;
        private string _kycStatus = string.Empty;
        private string _createdAt = string.Empty;
        private int _maxTunnelCount;
        private int _trafficLimit;
        private int _trafficUsed;
        private int _bandwidthLimit;
        private bool _hasKyc;
        private bool _isBaned;
        private bool _todayChecked;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string Avatar
        {
            get => _avatar;
            set { _avatar = value; OnPropertyChanged(); }
        }

        public string Role
        {
            get => _role;
            set { _role = value; OnPropertyChanged(); }
        }

        public string KycStatus
        {
            get => _kycStatus;
            set { _kycStatus = value; OnPropertyChanged(); }
        }

        public string CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(); }
        }

        public int MaxTunnelCount
        {
            get => _maxTunnelCount;
            set { _maxTunnelCount = value; OnPropertyChanged(); }
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

        public int BandwidthLimit
        {
            get => _bandwidthLimit;
            set { _bandwidthLimit = value; OnPropertyChanged(); }
        }

        public bool HasKyc
        {
            get => _hasKyc;
            set { _hasKyc = value; OnPropertyChanged(); }
        }

        public bool IsBaned
        {
            get => _isBaned;
            set { _isBaned = value; OnPropertyChanged(); }
        }

        public bool TodayChecked
        {
            get => _todayChecked;
            set { _todayChecked = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 流量使用百分比
        /// </summary>
        public double TrafficUsagePercentage => TrafficLimit > 0 ? (double)TrafficUsed / TrafficLimit * 100 : 0;

        /// <summary>
        /// 剩余流量
        /// </summary>
        public int TrafficRemaining => TrafficLimit - TrafficUsed;
    }
}

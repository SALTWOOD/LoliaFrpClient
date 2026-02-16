using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LoliaFrpClient.Models
{
    /// <summary>
    /// 隧道视图模型
    /// </summary>
    public class TunnelViewModel : INotifyPropertyChanged
    {
        private int _id;
        private string _name = string.Empty;
        private string _type = string.Empty;
        private string _status = string.Empty;
        private string _remark = string.Empty;
        private string _customDomain = string.Empty;
        private string _localIp = string.Empty;
        private int _localPort;
        private int _remotePort;
        private int _nodeId;
        private int _bandwidthLimit;

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

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusBrush)); OnPropertyChanged(nameof(StatusDisplayText)); }
        }

        public string Remark
        {
            get => _remark;
            set { _remark = value; OnPropertyChanged(); }
        }

        public string CustomDomain
        {
            get => _customDomain;
            set { _customDomain = value; OnPropertyChanged(); }
        }

        public string LocalIp
        {
            get => _localIp;
            set { _localIp = value; OnPropertyChanged(); }
        }

        public int LocalPort
        {
            get => _localPort;
            set { _localPort = value; OnPropertyChanged(); }
        }

        public int RemotePort
        {
            get => _remotePort;
            set { _remotePort = value; OnPropertyChanged(); }
        }

        public int NodeId
        {
            get => _nodeId;
            set { _nodeId = value; OnPropertyChanged(); }
        }

        public int BandwidthLimit
        {
            get => _bandwidthLimit;
            set { _bandwidthLimit = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 状态显示文本
        /// </summary>
        public string StatusDisplayText
        {
            get
            {
                return Status switch
                {
                    "active" => "运行中",
                    "inactive" => "未激活",
                    "disabled" => "已禁用",
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
                    "active" => new SolidColorBrush(Microsoft.UI.Colors.Green),
                    "inactive" => new SolidColorBrush(Microsoft.UI.Colors.Gray),
                    "disabled" => new SolidColorBrush(Microsoft.UI.Colors.Red),
                    _ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
            }
        }

        /// <summary>
        /// 类型显示文本
        /// </summary>
        public string TypeDisplayText
        {
            get
            {
                return Type switch
                {
                    "tcp" => "TCP",
                    "udp" => "UDP",
                    "http" => "HTTP",
                    "https" => "HTTPS",
                    _ => Type.ToUpper()
                };
            }
        }
    }
}

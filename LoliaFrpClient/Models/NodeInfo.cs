using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace LoliaFrpClient.Models;

/// <summary>
///     节点信息视图模型
/// </summary>
public class NodeInfo : INotifyPropertyChanged
{
    private string _agentVersion = string.Empty;
    private int _bandwidth;
    private string _frpsVersion = string.Empty;
    private string _host = string.Empty;
    private int _id;
    private string _lastSeen = string.Empty;
    private string _location = string.Empty;
    private string _name = string.Empty;
    private int _port;
    private string _sponsor = string.Empty;
    private string _status = string.Empty;
    private string _supportedProtocols = string.Empty;

    public int Id
    {
        get => _id;
        set
        {
            _id = value;
            OnPropertyChanged();
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public string Location
    {
        get => _location;
        set
        {
            _location = value;
            OnPropertyChanged();
        }
    }

    public string Host
    {
        get => _host;
        set
        {
            _host = value;
            OnPropertyChanged();
        }
    }

    public int Port
    {
        get => _port;
        set
        {
            _port = value;
            OnPropertyChanged();
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(Online));
        }
    }

    public int Bandwidth
    {
        get => _bandwidth;
        set
        {
            _bandwidth = value;
            OnPropertyChanged();
        }
    }

    public bool Online => _status == "online";

    public string AgentVersion
    {
        get => _agentVersion;
        set
        {
            _agentVersion = value;
            OnPropertyChanged();
        }
    }

    public string FrpsVersion
    {
        get => _frpsVersion;
        set
        {
            _frpsVersion = value;
            OnPropertyChanged();
        }
    }

    public string Sponsor
    {
        get => _sponsor;
        set
        {
            _sponsor = value;
            OnPropertyChanged();
        }
    }

    public string LastSeen
    {
        get => _lastSeen;
        set
        {
            _lastSeen = value;
            OnPropertyChanged();
        }
    }

    public string SupportedProtocols
    {
        get => _supportedProtocols;
        set
        {
            _supportedProtocols = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     状态显示文本
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
    ///     状态颜色画刷
    /// </summary>
    public Brush StatusBrush
    {
        get
        {
            return Status switch
            {
                "online" => new SolidColorBrush(Colors.Green),
                "offline" => new SolidColorBrush(Colors.Red),
                "maintenance" => new SolidColorBrush(Colors.Orange),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
    }

    /// <summary>
    ///     显示名称（用于ComboBox显示）
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
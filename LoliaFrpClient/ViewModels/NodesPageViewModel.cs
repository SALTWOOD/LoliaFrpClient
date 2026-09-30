using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoliaFrpClient.Core;

namespace LoliaFrpClient.ViewModels;

// Nodes and tunnel creation share one page: the node list is the picker for the form,
// and both need the same live data.
public sealed partial class NodesPageViewModel : ViewModelBase
{
    public NodesPageViewModel()
    {
        Types = ["tcp", "udp", "http", "https"];
        SelectedType = Types[0];
    }

    /// <summary>窄屏下是否进入创建子页。宽屏下表单常驻,不使用这个开关。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormVisible))]
    [NotifyPropertyChangedFor(nameof(ListVisible))]
    public partial bool IsFormOpen { get; set; }

    // 宽屏:列表在左、表单在右,两者常驻并列。
    // 窄屏:两者互斥 —— 进创建子页就藏起列表。
    //
    // 早先的做法是让表单叠在列表上面,靠卡片背景盖住它。那条路不可靠:
    // 卡片背景只覆盖自身的边界,内容一旦溢出就把下面的列表透出来,两边的文字会互相穿插。
    // Grid 的 Row/Column/ColumnSpan 与 Width 都是可绑定的,所以一套标记就能同时满足两种排布。
    // IsCompact 只在启动时设定一次,因此这些派生属性无需变更通知。
    public int ListColumnSpan => IsCompact ? 2 : 1;

    public int FormColumn => IsCompact ? 0 : 1;

    public int FormColumnSpan => IsCompact ? 2 : 1;

    // NaN 在 Avalonia 里表示「不约束」,窄屏让表单自己撑满。
    public double FormWidth => IsCompact ? double.NaN : 380;

    public bool FormVisible => !IsCompact || IsFormOpen;

    public bool ListVisible => !IsCompact || !IsFormOpen;

    // 窄屏下点节点即进入创建子页 —— 表单本来就必须先选节点才能提交,
    // 所以不再单设「新建」入口,选中就是进入。宽屏两侧同时可见,不需要跳。
    //
    // 只在用户真正点击时才跳:LoadAsync 结尾的预选也会走到这里,
    // 不区分的话「进节点页」和「回节点页」都会直接落到创建子页上。
    private bool _restoringSelection;

    partial void OnSelectedNodeChanged(NodeRow? value)
    {
        if (IsCompact && !_restoringSelection && value is not null) IsFormOpen = true;
    }

    [RelayCommand]
    private void GoBack()
    {
        IsFormOpen = false;
    }

    public ObservableCollection<NodeRow> Nodes { get; } = [];

    public IReadOnlyList<string> Types { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyPropertyChangedFor(nameof(NodeWarning))]
    [NotifyPropertyChangedFor(nameof(HasNodeWarning))]
    [NotifyPropertyChangedFor(nameof(SelectedNodeText))]
    public partial NodeRow? SelectedNode { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyPropertyChangedFor(nameof(UsesCustomDomain))]
    [NotifyPropertyChangedFor(nameof(UsesRemotePort))]
    [NotifyPropertyChangedFor(nameof(NodeWarning))]
    [NotifyPropertyChangedFor(nameof(HasNodeWarning))]
    public partial string SelectedType { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial string LocalIp { get; set; } = "127.0.0.1";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial string LocalPortText { get; set; } = string.Empty;

    [ObservableProperty] public partial string RemotePortText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial string CustomDomain { get; set; } = string.Empty;

    [ObservableProperty] public partial string Remark { get; set; } = string.Empty;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial bool IsCreating { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusMessage { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    // http/https take a domain; tcp/udp take an optional remote port.
    public bool UsesCustomDomain => SelectedType is "http" or "https";

    public bool UsesRemotePort => !UsesCustomDomain;

    public string? NodeWarning => SelectedNode is not { } node
        ? null
        : !node.IsOnline
            ? "该节点当前离线,无法在其上创建隧道。"
            : !node.Info.Supports(SelectedType)
                ? $"该节点不支持 {SelectedType.ToUpperInvariant()} 协议。"
                : node.NeedKyc
                    ? "该节点要求先完成实名认证。"
                    : null;

    public bool HasNodeWarning => NodeWarning is not null;

    public string SelectedNodeText => SelectedNode is { } node
        ? $"{node.Name}({node.StatusText} · 负载 {node.LoadText})"
        : "请在左侧选择一个节点";

    public override Task ActivateAsync()
    {
        return LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            if (!ApiSession.Current.IsAuthenticated)
            {
                Nodes.Clear();
                ErrorMessage = "尚未登录,请前往「设置」完成登录。";
                return;
            }

            var result = await Node.ListAsync().ConfigureAwait(true);

            if (result is not { IsSuccess: true, Data: { } nodes })
            {
                ErrorMessage = result.Msg;
                return;
            }

            var previouslySelected = SelectedNode?.Id;
            Nodes.Clear();

            // Online first, then least loaded: the top of the list is the sensible default.
            foreach (var info in nodes
                         .OrderByDescending(n => n.IsOnline)
                         .ThenBy(n => n.Load ?? double.MaxValue)
                         .ThenBy(n => n.Id))
                Nodes.Add(new NodeRow(info));

            // 预选是程序行为,不能当成「用户点了这个节点」——否则一进页面就会因为
            // 预选而直接跳进创建子页。用标志位把这次赋值与用户点击区分开。
            _restoringSelection = true;
            try
            {
                SelectedNode = Nodes.FirstOrDefault(n => n.Id == previouslySelected)
                               ?? Nodes.FirstOrDefault(n => n.IsOnline)
                               ?? Nodes.FirstOrDefault();
            }
            finally
            {
                _restoringSelection = false;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        if (SelectedNode is not { } node || !TryPort(LocalPortText, out var localPort)) return;

        int? remotePort = null;
        if (UsesRemotePort && !string.IsNullOrWhiteSpace(RemotePortText))
        {
            if (!TryPort(RemotePortText, out var parsed))
            {
                ErrorMessage = "远程端口必须是 1-65535 之间的整数。";
                return;
            }

            remotePort = parsed;
        }

        var domain = CustomDomain.Trim();
        var remark = Remark.Trim();

        if (UsesCustomDomain && domain.Length > 256)
        {
            ErrorMessage = "域名最长 256 个字符。";
            return;
        }

        if (remark.Length > 500)
        {
            ErrorMessage = "备注最长 500 个字符。";
            return;
        }

        IsCreating = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var result = await Tunnel.CreateAsync(new CreateTunnelSpec
            {
                NodeId = node.Id,
                Type = SelectedType,
                LocalIp = LocalIp.Trim(),
                LocalPort = localPort,
                RemotePort = remotePort,
                CustomDomain = UsesCustomDomain ? domain : null,
                Remark = remark.Length == 0 ? null : remark
            }).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                ErrorMessage = $"创建失败:{result.Msg}";
                return;
            }

            StatusMessage = $"已创建隧道 {result.Data?.Name}";
            RemotePortText = string.Empty;
            CustomDomain = string.Empty;
            Remark = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsCreating = false;
        }
    }

    private bool CanCreate()
    {
        return !IsCreating &&
               SelectedNode is { IsOnline: true } node &&
               node.Info.Supports(SelectedType) &&
               !string.IsNullOrWhiteSpace(LocalIp) &&
               TryPort(LocalPortText, out _) &&
               (!UsesCustomDomain || !string.IsNullOrWhiteSpace(CustomDomain));
    }

    private static bool TryPort(string? text, out int port)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) &&
               port is >= 1 and <= 65535;
    }
}

public sealed class NodeRow
{
    public NodeRow(NodeInfo info)
    {
        Info = info;
    }

    public NodeInfo Info { get; }

    public int Id => Info.Id;

    public string Name => Info.Name;

    public bool IsOnline => Info.IsOnline;

    public bool IsOffline => !Info.IsOnline;

    public string StatusText => Info.IsOnline ? "在线" : "离线";

    public string RegionText =>
        string.IsNullOrWhiteSpace(Info.RegionCode) ? "—" : Info.RegionCode!.ToUpperInvariant();

    public string ProtocolsText => Info.SupportedProtocols.Count == 0
        ? "—"
        : string.Join(" / ", Info.SupportedProtocols.Select(p => p.ToUpperInvariant()));

    // Kept short on purpose: the version string the API returns is long enough to wrap the
    // row onto a second line, which makes every item a different height.
    public string SummaryText
    {
        get
        {
            var parts = new List<string> { ProtocolsText };

            if (Info.Bandwidth is { } bandwidth) parts.Add($"{bandwidth} Mbps");

            return string.Join(" · ", parts);
        }
    }

    // ProgressBar wants a plain double; nodes without reported metrics show a dash instead.
    public double LoadPercent => Info.Load ?? 0;

    public bool HasLoad => Info.Load.HasValue;

    public string LoadText => Info.Load is { } load ? $"{load:0.#}%" : "无数据";

    public bool HighTraffic => Info.HighTraffic;

    public bool NeedKyc => Info.NeedKyc;

    public bool BeianRequired => Info.BeianRequired;

    public bool HasSponsor => !string.IsNullOrWhiteSpace(Info.Sponsor);

    public string SponsorText => Info.Sponsor ?? string.Empty;

    // A ratio of exactly 1 costs nothing extra and is not worth flagging.
    public bool HasTrafficRatio => Info.TrafficRatio is { } ratio && Math.Abs(ratio - 1) > 0.001;

    public string TrafficRatioText => Info.TrafficRatio is { } ratio ? $"{ratio:0.##}x 计费" : string.Empty;

    public bool HasRemark => !string.IsNullOrWhiteSpace(Info.Remark);

    public string RemarkText => Info.Remark ?? string.Empty;
}
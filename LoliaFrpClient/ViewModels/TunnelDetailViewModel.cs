using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoliaFrpClient.Core;

namespace LoliaFrpClient.ViewModels;

public sealed partial class TunnelDetailViewModel : ObservableObject
{
    private readonly string _tunnelName;

    public TunnelDetailViewModel(string tunnelName)
    {
        _tunnelName = tunnelName;
    }

    // The caller hooks this to show a confirmation before anything destructive happens.
    public event Action? DeleteRequested;

    public event Action? CloseRequested;

    // Set once the tunnel was renamed or deleted, so the list behind the window reloads.
    public bool Changed { get; private set; }

    public ObservableCollection<DetailRow> Rows { get; } = [];

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(DeleteHint))]
    public partial TunnelDetail? Detail { get; set; }

    [ObservableProperty] public partial string Remark { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public string Title => Detail?.Remark is { Length: > 0 } remark ? remark : Detail?.Name ?? _tunnelName;

    // Deleting is irreversible and cannot be undone from inside the app, so say what it costs.
    public string DeleteHint => Detail?.IsActive == true
        ? "该隧道当前在线,删除会立即断开连接。此操作不可撤销。"
        : "此操作不可撤销。";

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await new Tunnel(_tunnelName).GetDetailInfoAsync().ConfigureAwait(true);

            if (!result.IsSuccess || result.Data is null)
            {
                ErrorMessage = result.Msg;
                return;
            }

            Detail = result.Data;
            Remark = result.Data.Remark ?? string.Empty;
            RebuildRows();
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

    [RelayCommand]
    private async Task SaveRemarkAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var trimmed = Remark.Trim();

            if (trimmed.Length > 500)
            {
                ErrorMessage = "备注最长 500 个字符。";
                return;
            }

            var result = await new Tunnel(_tunnelName)
                .UpdateRemarkAsync(trimmed.Length == 0 ? null : trimmed)
                .ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                ErrorMessage = $"保存失败:{result.Msg}";
                return;
            }

            Changed = true;
            StatusMessage = "备注已保存";

            // Re-read so the header and rows reflect what the server actually stored.
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RequestDelete()
    {
        DeleteRequested?.Invoke();
    }

    // Called by the window once the user has confirmed.
    public async Task DeleteAsync()
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await new Tunnel(_tunnelName).DeleteAsync().ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                ErrorMessage = $"删除失败:{result.Msg}";
                return;
            }

            Changed = true;
            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke();
    }

    private void RebuildRows()
    {
        Rows.Clear();

        if (Detail is not { } detail) return;

        Add("隧道名", detail.Name);
        Add("类型", detail.Type?.ToUpperInvariant());
        Add("节点", detail.NodeName);
        Add("节点地址", detail.NodeAddress);
        Add("本地入口", detail.LocalEndpoint);
        Add("远端入口", detail.RemoteEndpoint);
        Add("状态", detail.IsActive ? "在线" : "已下线");
        Add("隧道 ID", detail.Id > 0 ? detail.Id.ToString() : null);
        Add("带宽限制", detail.BandwidthLimit is { } bandwidth ? $"{bandwidth} Mbps" : null);
        Add("客户端版本", detail.ClientVersion);
        Add("创建时间", detail.CreatedAt);
        Add("隧道 Token", detail.MaskedToken);
    }

    private void Add(string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) Rows.Add(new DetailRow(label, value));
    }
}

public sealed record DetailRow(string Label, string Value);
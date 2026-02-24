using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LoliaFrpClient.Services;

/// <summary>
///     对话框管理器，使用信号量确保同一时间只有一个 ContentDialog 显示
/// </summary>
public sealed class DialogManager
{
    private static DialogManager? _instance;
    private static readonly object _lock = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    ///     获取 DialogManager 的单例实例
    /// </summary>
    public static DialogManager Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                {
                    _instance ??= new DialogManager();
                }

            return _instance;
        }
    }

    /// <summary>
    ///     显示 ContentDialog 并等待结果（排队机制）
    /// </summary>
    /// <param name="dialog">要显示的 ContentDialog</param>
    /// <returns>对话框结果</returns>
    public async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        await _semaphore.WaitAsync();
        try
        {
            // 确保 XamlRoot 已设置
            if (dialog.XamlRoot == null && App.MainWindow?.Content != null)
                dialog.XamlRoot = App.MainWindow.Content.XamlRoot;

            return await dialog.ShowAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    ///     创建并显示一个简单的消息对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息内容</param>
    /// <param name="closeButtonText">关闭按钮文本</param>
    /// <returns>对话框结果</returns>
    public async Task<ContentDialogResult> ShowMessageAsync(string title, string message,
        string closeButtonText = "确定")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = closeButtonText
        };

        return await ShowDialogAsync(dialog);
    }

    /// <summary>
    ///     创建并显示一个确认对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息内容</param>
    /// <param name="primaryButtonText">主按钮文本</param>
    /// <param name="closeButtonText">关闭按钮文本</param>
    /// <returns>对话框结果</returns>
    public async Task<ContentDialogResult> ShowConfirmAsync(string title, string message,
        string primaryButtonText = "确定", string closeButtonText = "取消")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary
        };

        return await ShowDialogAsync(dialog);
    }

    /// <summary>
    ///     显示错误对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">错误消息</param>
    /// <returns>对话框结果</returns>
    public async Task<ContentDialogResult> ShowErrorAsync(string title, string message)
    {
        return await ShowMessageAsync(title, message, "确定");
    }
}

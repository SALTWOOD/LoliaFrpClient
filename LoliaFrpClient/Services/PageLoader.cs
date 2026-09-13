using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace LoliaFrpClient.Services;

/// <summary>
///     页面通用的加载 / 错误处理骨架，消除各页面重复的 try-catch-loading 样板。
/// </summary>
public static class PageLoader
{
    /// <summary>
    ///     执行一次数据加载：先置为加载中，失败时按 <paramref name="errorTitle"/> 弹窗
    ///     （登录失效则静默），最后恢复非加载态。
    /// </summary>
    /// <param name="setLoadingState">用于切换页面自身控件的加载态。</param>
    /// <param name="load">实际的数据加载动作。</param>
    /// <param name="errorTitle">失败弹窗的标题。</param>
    public static async Task RunAsync(Action<bool> setLoadingState, Func<Task> load, string errorTitle)
    {
        setLoadingState(true);

        try
        {
            await load();
        }
        catch (Exception ex)
        {
            if (AuthErrorHelper.ShouldSilence(ex)) return;

            await DialogManager.Instance.ShowErrorAsync(errorTitle, ex.Message);
        }
        finally
        {
            setLoadingState(false);
        }
    }
}

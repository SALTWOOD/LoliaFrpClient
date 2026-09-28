using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LoliaFrpClient.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    ///     页面被导航到时调用,用于取数。
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         页面的 ViewModel 由 <see cref="MainViewModel" /> 长期持有,不会每次导航都重建,
    ///         所以「进入页面就刷新」必须靠这个钩子,不能靠构造函数。
    ///     </para>
    ///     <para>
    ///         实现不得向外抛异常:调用方是 UI 事件处理器,异常会直接终结进程。
    ///         失败一律捕获后写进各自的错误状态。
    ///     </para>
    /// </remarks>
    public virtual Task ActivateAsync() => Task.CompletedTask;
}

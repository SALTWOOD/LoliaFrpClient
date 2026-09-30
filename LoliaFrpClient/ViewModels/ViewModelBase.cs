using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LoliaFrpClient.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>
    ///     当前是否走窄屏布局。由外壳设定,页面据此把横排内容折成单列。
    /// </summary>
    /// <remarks>
    ///     移动端恒为 <c>true</c>,桌面端恒为 <c>false</c>——桌面窗口最小宽度 880,
    ///     横排内容放得下;手机无论横竖屏都该走单列。因此这个开关目前不随窗口尺寸变化,
    ///     只是个平台开关,桌面端的既有观感因此完全不受影响。
    /// </remarks>
    [ObservableProperty]
    public partial bool IsCompact { get; set; }

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
    public virtual Task ActivateAsync()
    {
        return Task.CompletedTask;
    }
}
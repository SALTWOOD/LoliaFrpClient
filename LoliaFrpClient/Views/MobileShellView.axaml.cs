using Avalonia;
using Avalonia.Controls;
using LoliaFrpClient.Services;
using LoliaFrpClient.ViewModels;

namespace LoliaFrpClient.Views;

/// <summary>
/// 移动端外壳:内容区在上面,导航是底栏。桌面端对应的是 <see cref="MainWindow" /> 的左侧栏。
/// </summary>
public partial class MobileShellView : UserControl
{
    public MobileShellView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        HookSafeArea();

        if (DataContext is MainViewModel viewModel)
        {
            // 移动端恒为窄屏:手机竖屏 360dp,横屏也才 800dp 左右,都不该用横排布局。
            viewModel.UseCompactLayout();

            // 首屏不经过导航事件,补一次取数。
            await viewModel.RefreshAsync();
        }

        // 放在取数之后:更新是次要的,别拖慢首屏。
        if (AppSettings.Current.AutoCheckUpdates) await UpdatePrompt.CheckAndShowAsync(this);
    }

    // Android 的手势条/导航栏会压在底栏上,平台给出的安全区要补到 padding 里。
    private void HookSafeArea()
    {
        if (TopLevel.GetTopLevel(this)?.InsetsManager is not { } insets) return;

        ApplySafeArea(insets.SafeAreaPadding);
        insets.SafeAreaChanged += (_, args) => ApplySafeArea(args.SafeAreaPadding);
    }

    private void ApplySafeArea(Thickness padding)
    {
        NavBar.Padding = new Thickness(0, 0, 0, padding.Bottom);
    }
}
using System;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.System;
using LoliaFrpClient.Pages;
using LoliaFrpClient.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LoliaFrpClient;

/// <summary>
///     Main window with Mica UI, sidebar navigation, and dark mode support
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly SettingsStorage _settings = SettingsStorage.Instance;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTitleBar();
        InitializeTheme();
        InitializeNavigation();

        // 窗口关闭时清理资源
        Closed += OnWindowClosed;

        // 根据设置决定是否在启动时检查客户端更新
        if (_settings.AutoCheckClientUpdate) _ = CheckForClientUpdateAsync();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        // 释放 FrpcManager，这会关闭 Job Object 句柄
        // 由于设置了 JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE，所有子进程会自动终止
        ServiceLocator.FrpcManager.Dispose();
    }

    private void InitializeTitleBar()
    {
        var appWindow = GetAppWindowForCurrentWindow();
        if (appWindow != null)
        {
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonForegroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonHoverBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
            appWindow.TitleBar.ButtonPressedBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;

            // Set drag region for title bar
            if (TitleBarGrid != null)
                TitleBarGrid.SizeChanged += (s, e) =>
                {
                    if (TitleBarGrid != null && BackButton != null)
                    {
                        // Exclude the button area from drag region
                        var buttonWidth = (int)BackButton.ActualWidth + 8; // Button width + margin
                        var dragRect = new RectInt32(buttonWidth, 0, (int)TitleBarGrid.ActualWidth - buttonWidth,
                            (int)TitleBarGrid.ActualHeight);
                        appWindow.TitleBar.SetDragRectangles(new[] { dragRect });
                    }
                };
        }
    }

    private AppWindow GetAppWindowForCurrentWindow()
    {
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    private void InitializeTheme()
    {
        ApplyTheme(_settings.IsDarkMode);
    }

    private void InitializeNavigation()
    {
        MainNavigationView.ItemInvoked += OnNavigationViewItemInvoked;
        ContentFrame.Navigate(typeof(UserInfoPage));
        MainNavigationView.SelectedItem = Page1NavItem;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
            UpdateNavigationViewSelection();
        }
    }

    private void UpdateNavigationViewSelection()
    {
        if (ContentFrame.Content == null) return;

        var currentPageType = ContentFrame.Content.GetType();
        NavigationViewItem? selectedItem = null;

        if (currentPageType == typeof(UserInfoPage))
            selectedItem = Page1NavItem;
        else if (currentPageType == typeof(TunnelListPage))
            selectedItem = Page2NavItem;
        else if (currentPageType == typeof(FrpcManagerPage)) selectedItem = FrpcManagerNavItem;

        if (selectedItem != null) MainNavigationView.SelectedItem = selectedItem;
    }

    private void OnNavigationViewItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer != null && args.InvokedItemContainer.Tag != null)
        {
            var tag = args.InvokedItemContainer.Tag.ToString();

            switch (tag)
            {
                case "Page1":
                    ContentFrame.Navigate(typeof(UserInfoPage));
                    break;
                case "Page2":
                    ContentFrame.Navigate(typeof(TunnelListPage));
                    break;
                case "FrpcManager":
                    ContentFrame.Navigate(typeof(FrpcManagerPage));
                    break;
                case "Theme":
                    ToggleTheme();
                    break;
                case "Settings":
                    ContentFrame.Navigate(typeof(Settings));
                    break;
            }
        }
    }
    
    public static void NavigateTo(Type pageType)
    {
        (App.MainWindow as MainWindow)?.ContentFrame.Navigate(pageType);
    }
    
    public static void NavigateTo<T>() => NavigateTo(typeof(T));

    private void ToggleTheme()
    {
        var isDarkMode = !_settings.IsDarkMode;
        _settings.IsDarkMode = isDarkMode;
        ApplyTheme(isDarkMode);
    }

    private void ApplyTheme(bool isDarkMode)
    {
        if (RootGrid != null)
        {
            if (isDarkMode)
                RootGrid.RequestedTheme = ElementTheme.Dark;
            else
                RootGrid.RequestedTheme = ElementTheme.Light;
        }
    }

    /// <summary>
    ///     启动时检查客户端更新
    /// </summary>
    private async Task CheckForClientUpdateAsync()
    {
        try
        {
            var updateResult = await ClientUpdateService.CheckForUpdateAsync();

            if (updateResult.HasUpdate) await ShowUpdateDialogAsync(updateResult);
        }
        catch (Exception)
        {
            // 静默失败，不影响启动
        }
    }

    /// <summary>
    ///     显示更新提示对话框
    /// </summary>
    private async Task ShowUpdateDialogAsync(ClientUpdateResult updateResult)
    {
        var dialog = new ContentDialog
        {
            Title = "发现新版本",
            Content =
                $"当前版本: {updateResult.CurrentVersion}\n最新版本: {updateResult.LatestVersion}\n\n{TruncateReleaseNotes(updateResult.ReleaseNotes, 200)}",
            PrimaryButtonText = "前往下载",
            CloseButtonText = "稍后提醒"
        };

        var result = await DialogManager.Instance.ShowDialogAsync(dialog);

        if (result == ContentDialogResult.Primary)
        {
            // 打开 GitHub Release 页面
            var uri = new Uri(updateResult.ReleaseUrl);
            await Launcher.LaunchUriAsync(uri);
        }
    }

    /// <summary>
    ///     截断更新日志
    /// </summary>
    private static string TruncateReleaseNotes(string notes, int maxLength)
    {
        if (string.IsNullOrEmpty(notes))
            return "";

        if (notes.Length <= maxLength)
            return notes;

        return notes.Substring(0, maxLength) + "...";
    }
}
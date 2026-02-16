using LoliaFrpClient.Pages;
using LoliaFrpClient.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LoliaFrpClient
{
    /// <summary>
    /// Main window with Mica UI, sidebar navigation, and dark mode support
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
                {
                    TitleBarGrid.SizeChanged += (s, e) =>
                    {
                        if (TitleBarGrid != null && BackButton != null)
                        {
                            // Exclude the button area from drag region
                            int buttonWidth = (int)BackButton.ActualWidth + 8; // Button width + margin
                            RectInt32 dragRect = new RectInt32(buttonWidth, 0, (int)TitleBarGrid.ActualWidth - buttonWidth, (int)TitleBarGrid.ActualHeight);
                            appWindow.TitleBar.SetDragRectangles(new[] { dragRect });
                        }
                    };
                }
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
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

            Type? currentPageType = ContentFrame.Content.GetType();
            NavigationViewItem? selectedItem = null;

            if (currentPageType == typeof(UserInfoPage))
            {
                selectedItem = Page1NavItem;
            }
            else if (currentPageType == typeof(TunnelListPage))
            {
                selectedItem = Page2NavItem;
            }
            else if (currentPageType == typeof(TrafficStatsPage))
            {
                selectedItem = Page3NavItem;
            }
            else if (currentPageType == typeof(Page4))
            {
                selectedItem = Page4NavItem;
            }

            if (selectedItem != null)
            {
                MainNavigationView.SelectedItem = selectedItem;
            }
        }

        private void OnNavigationViewItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null && args.InvokedItemContainer.Tag != null)
            {
                string? tag = args.InvokedItemContainer.Tag.ToString();

                switch (tag)
                {
                    case "Page1":
                        ContentFrame.Navigate(typeof(UserInfoPage));
                        break;
                    case "Page2":
                        ContentFrame.Navigate(typeof(TunnelListPage));
                        break;
                    case "Page3":
                        ContentFrame.Navigate(typeof(TrafficStatsPage));
                        break;
                    case "Page4":
                        ContentFrame.Navigate(typeof(Page4));
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

        private void ToggleTheme()
        {
            bool isDarkMode = !_settings.IsDarkMode;
            _settings.IsDarkMode = isDarkMode;
            ApplyTheme(isDarkMode);
        }

        private void ApplyTheme(bool isDarkMode)
        {
            if (RootGrid != null)
            {
                if (isDarkMode)
                {
                    RootGrid.RequestedTheme = ElementTheme.Dark;
                }
                else
                {
                    RootGrid.RequestedTheme = ElementTheme.Light;
                }
            }
        }
    }
}

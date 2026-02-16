using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using WinRT.Interop;
using LoliaFrpClient.Services;
using LoliaFrpClient.Pages;

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
                        if (TitleBarGrid != null)
                        {
                            RectInt32 dragRect = new RectInt32(0, 0, (int)TitleBarGrid.ActualWidth, (int)TitleBarGrid.ActualHeight);
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
            ContentFrame.Navigate(typeof(Page1));
            MainNavigationView.SelectedItem = Page1NavItem;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
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
                        ContentFrame.Navigate(typeof(Page1));
                        break;
                    case "Page2":
                        ContentFrame.Navigate(typeof(Page2));
                        break;
                    case "Page3":
                        ContentFrame.Navigate(typeof(Page3));
                        break;
                    case "Page4":
                        ContentFrame.Navigate(typeof(Page4));
                        break;
                    case "Settings":
                        ToggleTheme();
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

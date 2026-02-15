using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
            InitializeTheme();
            InitializeNavigation();
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

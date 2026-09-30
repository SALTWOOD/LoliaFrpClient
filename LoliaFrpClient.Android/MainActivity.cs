using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace LoliaFrpClient.Android;

[Activity(
    Label = "LoliaFrp",
    Theme = "@style/AppTheme",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation
                           | ConfigChanges.ScreenSize
                           | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize
                           | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
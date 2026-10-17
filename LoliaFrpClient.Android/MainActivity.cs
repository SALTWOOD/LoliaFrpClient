using System;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using LoliaFrpClient.Services;

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
    private const int NotificationPermissionRequest = 0x4C46;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        CrashReporting.Start();

        base.OnCreate(savedInstanceState);

        RequestNotificationPermission();
    }

    private void RequestNotificationPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return;

        var granted = CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications);
        if (granted == Permission.Granted) return;

        RequestPermissions([global::Android.Manifest.Permission.PostNotifications], NotificationPermissionRequest);
    }
}

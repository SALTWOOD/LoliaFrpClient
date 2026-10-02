using System;
using System.IO;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using LoliaFrpClient.Core.Frpc;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    public MainApplication(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();

        if (ApplicationInfo?.NativeLibraryDir is { } libraryDir)
            FrpcLocator.UseBundled(Path.Combine(libraryDir, "libfrpc.so"));

        // read-only file system
        if (FilesDir?.AbsolutePath is { } filesDir) FrpcLocator.WorkingDirectory = filesDir;

        TunnelKeepAlive.UseHost(new AndroidTunnelKeepAlive(this));
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .LogToTrace();
    }
}
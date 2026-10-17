using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Avalonia;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.Desktop;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CrashReporting.Start();

        if (OperatingSystem.IsMacOS())
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolveFromFrameworks;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static IntPtr ResolveFromFrameworks(Assembly assembly, string name)
    {
        var fileName = name.EndsWith(".dylib", StringComparison.Ordinal) ? name : name + ".dylib";
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Frameworks", fileName));

        return File.Exists(path) && NativeLibrary.TryLoad(path, out var handle) ? handle : IntPtr.Zero;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
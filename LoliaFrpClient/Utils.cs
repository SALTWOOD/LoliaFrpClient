using System.Runtime.InteropServices;
using System.Text;

namespace LoliaFrpClient;

public class Utils
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

    public static bool IsPackaged()
    {
        var length = 0;
        var sb = new StringBuilder(0);

        var result = GetCurrentPackageFullName(ref length, sb);

        return result != 15700;
    }


    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0.00 B";

        string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        var order = 0;

        double size = bytes;

        while (size >= 1024 && order < units.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.00} {units[order]}";
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LoliaFrpClient;

public class Utils
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

    public static bool IsPackaged()
    {
        int length = 0;
        StringBuilder sb = new StringBuilder(0);

        int result = GetCurrentPackageFullName(ref length, sb);

        return result != 15700;
    }


    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0.00 B";

        string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        int order = 0;

        double size = bytes;

        while (size >= 1024 && order < units.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.00} {units[order]}";
    }
}

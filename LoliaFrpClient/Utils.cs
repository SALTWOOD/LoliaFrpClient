using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoliaFrpClient;

public class Utils
{
    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";

        string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; // 没必要往后支持了，long 最大就 8EB
        int order = 0;

        while (bytes >= 1024 && order < units.Length - 1)
        {
            order++;
            bytes /= 1024;
        }
        return $"{bytes:0.00} {units[order]}";
    }
}

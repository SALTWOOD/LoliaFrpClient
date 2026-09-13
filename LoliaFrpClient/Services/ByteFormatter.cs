using System;

namespace LoliaFrpClient.Services;

/// <summary>
///     字节容量的人类可读格式化。全应用统一使用此实现，避免多处副本产生不一致输出。
/// </summary>
public static class ByteFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

    /// <summary>
    ///     将字节数格式化为两位小数的人类可读字符串，例如 1536 → "1.50 KB"。
    /// </summary>
    public static string Format(long bytes)
    {
        if (bytes <= 0) return "0.00 B";

        var order = 0;
        double size = bytes;

        while (size >= 1024 && order < Units.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.00} {Units[order]}";
    }
}

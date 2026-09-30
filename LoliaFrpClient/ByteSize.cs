using System;

namespace LoliaFrpClient;

/// <summary>
///     把字节数格式化成人读的流量文本。
/// </summary>
/// <remarks>
///     按 1024 进制换算,单位仍写作 KB/MB/GB。这不是笔误:服务端自己就是这么算的——
///     文档里 <c>traffic_bytes: 45097156608</c> 对应 <c>traffic_gb: 42</c>,正是除以 1024³。
///     显示时跟随服务端的口径,才不会出现「界面 41 GB、接口说 42 GB」这种对不上的情况。
/// </remarks>
internal static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>格式化字节数。<c>null</c> 或负数返回破折号。</summary>
    /// <param name="bytes">字节数。</param>
    /// <param name="maxDecimals">小数位上限。默认 1 位。</param>
    public static string Format(long? bytes, int maxDecimals = 1)
    {
        if (bytes is not { } value || value < 0) return "—";

        if (value == 0) return "0 B";

        var unit = 0;
        double size = value;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        // 到了三位数,小数位只是噪声;字节本身也没有小数。
        var decimals = unit == 0 || size >= 100 ? 0 : Math.Max(0, maxDecimals);
        var format = decimals == 0 ? "0" : "0." + new string('#', decimals);

        return $"{size.ToString(format)} {Units[unit]}";
    }
}
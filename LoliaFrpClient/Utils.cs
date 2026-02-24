using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LoliaFrpClient.Services;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Http.HttpClientLibrary;

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

public class AuthInterceptorHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // 在这里执行你的操作，例如记录日志或触发退出登录
            Console.WriteLine("LOG: Unauthorized access detected.");
            // 喵！可以在这里处理 Token 刷新逻辑
        }

        return response;
    }
}
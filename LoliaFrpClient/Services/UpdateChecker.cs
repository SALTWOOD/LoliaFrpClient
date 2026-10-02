using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LoliaFrpClient.Core;

namespace LoliaFrpClient.Services;

internal sealed record ClientUpdate(string Tag, Version Number);

// 更新检查是启动路径上的一步,任何失败都得当作「没有更新」咽掉,不能影响启动。
internal static class UpdateChecker
{
    private static readonly Assembly Self = Assembly.GetEntryAssembly() ?? typeof(UpdateChecker).Assembly;

    // InformationalVersion 保留 -beta 这类后缀(AssemblyVersion 会被 .NET 截掉,beta 版和正式版
    // 的 AssemblyVersion 一模一样),但它挂着 +<commit> 的构建元数据,去掉。
    private static readonly string FullText = ReadFullText();

    // 装的是预发布版就整个不检查:测试者不该被「升级到正式版」打扰。
    private static readonly bool IsPrerelease = FullText.Contains('-');

    // 只拿数字部分比较,1.0.9.0-beta 视作 1.0.9.0。
    private static readonly Version Current = ParseNumber(FullText) ?? new Version();

    public static string CurrentVersionText => $"v{FullText}";

    public static async Task<ClientUpdate?> TryCheckAsync(CancellationToken cancellationToken = default)
    {
        if (IsPrerelease) return null;

        // /releases/* 要鉴权,没登录就别白跑一趟(未带 token 是 401)。
        if (!ApiSession.Current.IsAuthenticated) return null;

        try
        {
            var result = await ClientRelease.GetLatestAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess || result.Data?.Tag is not { Length: > 0 } tag) return null;
            if (ParseNumber(tag) is not { } latest) return null;

            // 同号的 -beta 和正式版算同一个版本,不给已经是该号正式版的用户推它的 beta。
            return latest.CompareTo(Current) > 0 ? new ClientUpdate(tag, latest) : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ReadFullText()
    {
        var info = Self.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(info)) return (Self.GetName().Version ?? new Version()).ToString();

        return info.Split('+')[0];
    }

    private static Version? ParseNumber(string text)
    {
        return Version.TryParse(text.TrimStart('v', 'V').Split('-')[0], out var version) ? version : null;
    }
}

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LoliaFrpClient.Core;

namespace LoliaFrpClient.Services;

internal sealed record ClientUpdate(string Tag, Version Version);

// 更新检查是启动路径上的一步,任何失败都得当作「没有更新」咽掉,不能影响启动。
internal static class UpdateChecker
{
    public static Version CurrentVersion { get; } =
        (Assembly.GetEntryAssembly() ?? typeof(UpdateChecker).Assembly).GetName().Version ?? new Version();

    public static string CurrentVersionText => $"v{CurrentVersion}";

    public static async Task<ClientUpdate?> TryCheckAsync(CancellationToken cancellationToken = default)
    {
        // /releases/* 要鉴权,没登录就别白跑一趟(未带 token 是 401)。
        if (!ApiSession.Current.IsAuthenticated) return null;

        try
        {
            var result = await ClientRelease.GetLatestAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess || result.Data?.Tag is not { Length: > 0 } tag) return null;
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
            if (latest.CompareTo(CurrentVersion) <= 0) return null;

            return new ClientUpdate(tag, latest);
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
}

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LoliaFrpClient.Services;

internal static partial class FrpcVersionProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    // Returns null instead of throwing: an unrecognised build should not break the page,
    // and a wrong path is reported by the launch attempt itself.
    public static async Task<string?> TryReadAsync(string? frpcPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(frpcPath) || !File.Exists(frpcPath)) return null;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = frpcPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.StartInfo.ArgumentList.Add("-v");

            if (!process.Start()) return null;

            // Drain both streams concurrently before waiting: reading only one can block the
            // child once the other pipe fills.
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return null;
            }

            return Parse(await stdout.ConfigureAwait(false) + "\n" + await stderr.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException
                                       or UnauthorizedAccessException or OperationCanceledException)
        {
            return null;
        }
    }

    // Not assuming the whole output is the version: -v may append build info, or print to stderr.
    private static string? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = VersionPattern().Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
        }
    }

    [GeneratedRegex(@"(\d+\.\d+\.\d+[0-9A-Za-z.\-]*)")]
    private static partial Regex VersionPattern();
}
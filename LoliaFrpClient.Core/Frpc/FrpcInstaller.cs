using System.Diagnostics;

namespace LoliaFrpClient.Core.Frpc;

// Callers must stop any running frpc first: overwriting a running exe fails on Windows.
public sealed class FrpcInstaller
{
    private const int MaxAttempts = 3;

    // Rate-limits progress callbacks; without it the UI thread drowns.
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(200);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(1500);

    private readonly HttpClient _http;

    public FrpcInstaller(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    // LocalApplicationData rather than ApplicationData: this holds platform-specific
    // binaries that should not roam between machines.
    public static string GetManagedDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LoliaFrpClient",
            "frpc");
    }

    public static bool IsManagedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var full = Path.GetFullPath(path);
            var managed = Path.GetFullPath(GetManagedDirectory());
            return full.StartsWith(managed + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return false;
        }
    }

    public async Task<FrpcInstallResult> InstallAsync(
        FrpcInstallOptions options,
        IProgress<FrpcInstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        string? archivePath = null;

        try
        {
            progress?.Report(new FrpcInstallProgress
            {
                Stage = FrpcInstallStage.FetchingRelease,
                Message = "正在获取版本信息…"
            });

            var release = await FrpcReleaseClient.TryGetLatestAsync(_http, ct).ConfigureAwait(false);
            if (release is null) return Fail("无法获取版本信息,请检查网络连接");

            var selection = FrpcReleaseClient.SelectBestAsset(release);
            var workDirectory = ResolveWorkDirectory(options);
            Directory.CreateDirectory(workDirectory);

            // Fixed name with only the extension kept: the extractor dispatches on extension,
            // and a fixed name means failure cleanup does not have to guess the path.
            archivePath = Path.Combine(workDirectory, "frpc-download" + ExtensionOf(selection.Asset.Name));

            return await RunAsync(release, selection, options, workDirectory, archivePath, progress, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (archivePath is not null) TryDeleteFile(archivePath);

            return new FrpcInstallResult { Success = false, Canceled = true, Message = "已取消" };
        }
    }

    private async Task<FrpcInstallResult> RunAsync(
        FrpcRelease release,
        FrpcAssetSelection selection,
        FrpcInstallOptions options,
        string workDirectory,
        string archivePath,
        IProgress<FrpcInstallProgress>? progress,
        CancellationToken ct)
    {
        var asset = selection.Asset;
        var mirror = options.UseMirror
            ? string.IsNullOrWhiteSpace(options.Mirror) ? FrpcReleaseClient.DefaultMirror : options.Mirror
            : null;

        var primaryUrl = mirror is null ? asset.DownloadUrl : FrpcReleaseClient.ToMirrorUrl(asset.DownloadUrl, mirror);
        var canFallBack = !string.Equals(primaryUrl, asset.DownloadUrl, StringComparison.OrdinalIgnoreCase);
        var rounds = canFallBack ? 2 : 1;

        Exception? lastError = null;

        for (var round = 0; round < rounds; round++)
        {
            var url = round == 0 ? primaryUrl : asset.DownloadUrl;
            if (round > 0)
                progress?.Report(new FrpcInstallProgress
                {
                    Stage = FrpcInstallStage.Downloading,
                    Message = "镜像不可用,改从 GitHub 下载…"
                });

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
                try
                {
                    ct.ThrowIfCancellationRequested();
                    TryDeleteFile(archivePath);

                    progress?.Report(new FrpcInstallProgress
                    {
                        Stage = FrpcInstallStage.Downloading,
                        Message = attempt == 1 ? "正在下载…" : $"正在下载…(第 {attempt}/{MaxAttempts} 次)"
                    });

                    await DownloadAsync(url, archivePath, progress, ct).ConfigureAwait(false);

                    progress?.Report(new FrpcInstallProgress
                    {
                        Stage = FrpcInstallStage.Verifying,
                        Message = "正在校验…"
                    });

                    await FrpcChecksumVerifier.VerifyAsync(_http, release, asset, archivePath, mirror, ct)
                        .ConfigureAwait(false);

                    progress?.Report(new FrpcInstallProgress
                    {
                        Stage = FrpcInstallStage.Extracting,
                        Message = "正在解压…"
                    });

                    var frpcPath = await FrpcArchiveExtractor.ExtractAsync(archivePath, workDirectory, ct)
                        .ConfigureAwait(false);

                    progress?.Report(new FrpcInstallProgress
                    {
                        Stage = FrpcInstallStage.Completed,
                        Message = "完成"
                    });

                    return new FrpcInstallResult
                    {
                        Success = true,
                        FrpcPath = frpcPath,
                        Version = selection.Version
                    };
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException
                                               or TaskCanceledException)
                {
                    lastError = ex;
                    if (attempt < MaxAttempts) await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
                }
        }

        TryDeleteFile(archivePath);
        return Fail(lastError?.Message ?? "下载失败");
    }

    private async Task DownloadAsync(
        string url,
        string destination,
        IProgress<FrpcInstallProgress>? progress,
        CancellationToken ct)
    {
        using var response = await _http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        var received = 0L;
        var elapsed = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;

        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var target = new FileStream(
            destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        int read;

        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            received += read;

            if (elapsed.Elapsed - lastReport < ProgressInterval) continue;

            lastReport = elapsed.Elapsed;
            progress?.Report(new FrpcInstallProgress
            {
                Stage = FrpcInstallStage.Downloading,
                Message = "正在下载…",
                ReceivedBytes = received,
                TotalBytes = total,
                Percent = total > 0 ? received * 100d / total : 0,
                SpeedBytesPerSecond = elapsed.Elapsed.TotalSeconds > 0
                    ? received / elapsed.Elapsed.TotalSeconds
                    : 0
            });
        }
    }

    private static string ResolveWorkDirectory(FrpcInstallOptions options)
    {
        return string.IsNullOrWhiteSpace(options.WorkDirectory) ? GetManagedDirectory() : options.WorkDirectory;
    }

    private static string ExtensionOf(string assetName)
    {
        return assetName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ? ".tar.gz" : ".zip";
    }

    private static FrpcInstallResult Fail(string message)
    {
        return new FrpcInstallResult { Success = false, Message = message };
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
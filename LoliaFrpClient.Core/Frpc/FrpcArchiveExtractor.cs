using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace LoliaFrpClient.Core.Frpc;

internal static class FrpcArchiveExtractor
{
    public static string ExecutableName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "frpc.exe" : "frpc";

    public static async Task<string> ExtractAsync(string archivePath, string workDirectory, CancellationToken ct)
    {
        var extractDirectory = Path.Combine(workDirectory, "extract");
        if (Directory.Exists(extractDirectory)) Directory.Delete(extractDirectory, true);

        Directory.CreateDirectory(extractDirectory);

        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ZipFile.ExtractToDirectory(archivePath, extractDirectory);
        else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            await ExtractTarGzAsync(archivePath, extractDirectory, ct).ConfigureAwait(false);
        else
            throw new InvalidOperationException("不支持的压缩格式");

        // The executable sits in a versioned subdirectory whose name changes per release,
        // so search recursively rather than guessing the path.
        var executable = Directory.GetFiles(extractDirectory, ExecutableName, SearchOption.AllDirectories)
                             .FirstOrDefault()
                         ?? throw new InvalidOperationException($"压缩包里没有找到 {ExecutableName}");

        var destination = Path.Combine(workDirectory, ExecutableName);
        File.Copy(executable, destination, true);
        EnsureExecutable(destination);

        TryDeleteFile(archivePath);
        TryDeleteDirectory(extractDirectory);
        return destination;
    }

    private static async Task ExtractTarGzAsync(string gzFile, string extractDirectory, CancellationToken ct)
    {
        await using var file = File.OpenRead(gzFile);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);

        TarEntry? entry;
        while ((entry = await tar.GetNextEntryAsync(cancellationToken: ct).ConfigureAwait(false)) is not null)
        {
            var fullPath = Path.Combine(extractDirectory, entry.Name.TrimStart('.', '/'));

            switch (entry.EntryType)
            {
                case TarEntryType.Directory:
                    Directory.CreateDirectory(fullPath);
                    break;

                case TarEntryType.RegularFile:
                    var parent = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

                    await using (var output = File.Create(fullPath))
                    {
                        if (entry.DataStream is not null)
                            await entry.DataStream.CopyToAsync(output, ct).ConfigureAwait(false);
                    }

                    break;
            }
        }
    }

    // Neither zip nor tar extraction preserves the Unix execute bit, so set it explicitly.
    private static void EnsureExecutable(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            using var chmod = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/chmod",
                Arguments = $"+x \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            chmod?.WaitForExit(3000);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Not fatal: the eventual launch attempt reports the real error.
        }
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
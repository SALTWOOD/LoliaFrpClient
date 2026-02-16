using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Formats.Tar;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LoliaFrpClient.Services
{
    /// <summary>
    /// Frpc 进程信息
    /// </summary>
    public class FrpcProcessInfo
    {
        public Process Process { get; set; } = null!;
        public int TunnelId { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsRunning => !Process.HasExited;
    }

    /// <summary>
    /// Frpc 安装状态
    /// </summary>
    public enum FrpcInstallStatus
    {
        NotInstalled,
        Installed,
        Outdated
    }

    /// <summary>
    /// Frpc 管理器
    /// </summary>
    public class FrpcManager
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _frpcDirectory;
        private readonly string _frpcExecutablePath;

        private readonly ConcurrentDictionary<int, FrpcProcessInfo> _tunnelProcesses = new();

        private readonly SemaphoreSlim _installSemaphore = new SemaphoreSlim(1, 1);

        public string? InstalledVersion { get; private set; }

        public bool IsAnyProcessRunning => _tunnelProcesses.Values.Any(p => p.IsRunning);

        public FrpcManager(string? frpcDirectory = null)
        {
            if (!string.IsNullOrEmpty(frpcDirectory))
            {
                _frpcDirectory = frpcDirectory;
            }
            else
            {
                string baseDataPath = GetAppDataRoot();
                _frpcDirectory = Path.Combine(baseDataPath, "LoliaFrpClient", "frpc");
            }

            // 沟槽的路径映射
            Log($"[INIT] Frpc Directory: {_frpcDirectory}");

            if (!Directory.Exists(_frpcDirectory))
            {
                Directory.CreateDirectory(_frpcDirectory);
            }

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            _frpcExecutablePath = Path.Combine(_frpcDirectory, isWindows ? "frpc.exe" : "frpc");

            LoadInstalledVersion();
        }

        #region Path Adapter (Handle MSIX Virtualization)

        private string GetAppDataRoot()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    return Windows.Storage.ApplicationData.Current.LocalFolder.Path;
                }
                catch (InvalidOperationException)
                {
                    // 说明是普通 Win32 运行，没有程序包标识符
                    Log("[PATH] Running as unpackaged Win32 app.");
                }
                catch (Exception ex)
                {
                    Log($"[PATH] Error detecting package path: {ex.Message}");
                }
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        #endregion

        #region Version Management

        private void LoadInstalledVersion()
        {
            var versionFile = Path.Combine(_frpcDirectory, "version.txt");
            if (File.Exists(versionFile))
            {
                InstalledVersion = File.ReadAllText(versionFile).Trim();
            }
        }

        private void SaveInstalledVersion(string version)
        {
            var versionFile = Path.Combine(_frpcDirectory, "version.txt");
            File.WriteAllText(versionFile, version);
            InstalledVersion = version;
        }

        public bool IsFrpcReady() => File.Exists(_frpcExecutablePath);

        public FrpcInstallStatus GetInstallStatus(string? latestVersion = null)
        {
            if (!IsFrpcReady()) return FrpcInstallStatus.NotInstalled;
            if (!string.IsNullOrEmpty(latestVersion) && latestVersion != InstalledVersion)
                return FrpcInstallStatus.Outdated;
            return FrpcInstallStatus.Installed;
        }

        #endregion

        #region Download and Install

        public async Task<string> DownloadFrpcAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var tempDirectory = Path.Combine(_frpcDirectory, "temp");
            Directory.CreateDirectory(tempDirectory);

            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            var downloadPath = Path.Combine(tempDirectory, fileName);

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var totalBytesRead = 0L;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (totalBytes > 0)
                    progress?.Report((double)totalBytesRead / totalBytes);
            }

            return downloadPath;
        }

        public async Task<bool> InstallFrpcAsync(string downloadPath, string version)
        {
            // 使用信号量加锁，防止多线程同时安装导致文件占用
            await _installSemaphore.WaitAsync();
            try
            {
                var tempDir = Path.Combine(_frpcDirectory, "temp");
                var extractPath = Path.Combine(tempDir, $"extract_{Guid.NewGuid():N}");
                Directory.CreateDirectory(extractPath);

                Log($"[INSTALL] Extracting to: {extractPath}");

                if (downloadPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ZipFile.ExtractToDirectory(downloadPath, extractPath);
                }
                else if (downloadPath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                {
                    await using var fs = File.OpenRead(downloadPath);
                    await using var gzip = new GZipStream(fs, CompressionMode.Decompress);
                    // .NET 7+ 原生 TarFile 支持，不需要 TarInputStream 类了
                    await TarFile.ExtractToDirectoryAsync(gzip, extractPath, overwriteFiles: true);
                }

                var executableName = Path.GetFileName(_frpcExecutablePath);
                var foundFile = Directory.GetFiles(extractPath, executableName, SearchOption.AllDirectories).FirstOrDefault();

                if (foundFile == null) throw new FileNotFoundException("Could not find frpc binary in the downloaded package.");

                // 替换旧文件
                if (File.Exists(_frpcExecutablePath))
                {
                    File.Delete(_frpcExecutablePath);
                }
                File.Copy(foundFile, _frpcExecutablePath);

                // Linux/macOS 权限修复
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try { Process.Start("chmod", $"+x {_frpcExecutablePath}")?.WaitForExit(); } catch { /* ignore */ }
                }

                SaveInstalledVersion(version);

                // 清理临时文件
                try { Directory.Delete(tempDir, true); } catch { /* ignore */ }

                Log("[INSTALL] Frpc installed successfully.");
                return true;
            }
            finally
            {
                _installSemaphore.Release();
            }
        }

        public async Task<bool> UpdateFrpcAsync(string downloadUrl, string version, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            StopAllTunnelProcesses();
            var path = await DownloadFrpcAsync(downloadUrl, progress, cancellationToken);
            return await InstallFrpcAsync(path, version);
        }

        #endregion

        #region Process Control

        public bool StartTunnelProcess(int tunnelId, string? arguments = null)
        {
            if (_tunnelProcesses.TryGetValue(tunnelId, out var existing) && existing.IsRunning)
                throw new InvalidOperationException($"Tunnel {tunnelId} process is already running.");

            if (!IsFrpcReady()) throw new FileNotFoundException("Frpc binary is missing.");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _frpcExecutablePath,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _frpcDirectory // 显式设置工作目录
                };

                var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                process.Exited += (s, e) => OnTunnelProcessExited(tunnelId);

                if (process.Start())
                {
                    var info = new FrpcProcessInfo { Process = process, TunnelId = tunnelId, StartTime = DateTime.Now };
                    _tunnelProcesses[tunnelId] = info;
                    Log($"[PROCESS] Started tunnel {tunnelId}. PID: {process.Id}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to start frpc process: {ex.Message}", ex);
            }
        }

        public bool StopTunnelProcess(int tunnelId)
        {
            if (_tunnelProcesses.TryRemove(tunnelId, out var info))
            {
                try
                {
                    if (!info.Process.HasExited)
                    {
                        info.Process.Kill(true); // 递归杀死子进程
                        bool exited = info.Process.WaitForExit(5000);
                        Log($"[PROCESS] Stopped tunnel {tunnelId}. Success: {exited}");
                        return exited;
                    }
                }
                catch (Exception ex) { Log($"[ERROR] Stop process error: {ex.Message}"); }
                finally { info.Process.Dispose(); }
            }
            return true;
        }

        public void StopAllTunnelProcesses()
        {
            var ids = _tunnelProcesses.Keys.ToList();
            foreach (var id in ids)
            {
                StopTunnelProcess(id);
            }
        }

        #endregion

        #region Events and Helpers

        public event EventHandler<FrpcProcessInfo>? TunnelProcessExited;

        private void OnTunnelProcessExited(int tunnelId)
        {
            if (_tunnelProcesses.TryRemove(tunnelId, out var info))
            {
                Log($"[EVENT] Tunnel {tunnelId} exited.");
                TunnelProcessExited?.Invoke(this, info);
                info.Process.Dispose();
            }
        }

        public bool UninstallFrpc()
        {
            StopAllTunnelProcesses();
            try
            {
                if (File.Exists(_frpcExecutablePath)) File.Delete(_frpcExecutablePath);
                var versionFile = Path.Combine(_frpcDirectory, "version.txt");
                if (File.Exists(versionFile)) File.Delete(versionFile);
                InstalledVersion = null;
                Log("[UNINSTALL] Frpc uninstalled.");
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Uninstall failed: {ex.Message}");
            }
        }

        public string GetFrpcExecutablePath() => _frpcExecutablePath;
        public string GetFrpcDirectory() => _frpcDirectory;
        public bool IsTunnelProcessRunning(int tunnelId) => _tunnelProcesses.TryGetValue(tunnelId, out var p) && p.IsRunning;

        private void Log(string message)
        {
            Debug.WriteLine($"[FrpcManager] {message}");
        }

        #endregion
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Windows Job Object API用于管理子进程生命周期
    /// </summary>
    internal static class JobObjectApi
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll")]
        public static extern bool SetInformationJobObject(IntPtr hJob, JOBOBJECTINFOCLASS JobObjectInfoClass,ref JOBOBJECT_BASIC_LIMIT_INFORMATION lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll")]
        public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern bool TerminateJobObject(IntPtr hJob, uint uExitCode);
    }

    internal enum JOBOBJECTINFOCLASS
    {
        BasicLimitInformation = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public long Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    /// <summary>
    /// Frpc 进程信息
    /// </summary>
    public class FrpcProcessInfo
    {
        private bool _hasExited = false;
        
        public Process Process { get; set; } = null!;
        public int TunnelId { get; set; }
        public string TunnelName { get; set; } = string.Empty;
        public string TunnelRemark { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        
        public bool IsRunning
        {
            get
            {
                if (_hasExited) return false;
                try
                {
                    return Process != null && !Process.HasExited;
                }
                catch
                {
                    return false;
                }
            }
        }
        
        public void MarkAsExited()
        {
            _hasExited = true;
        }
        
        public ObservableCollection<string> LogOutput { get; } = new ObservableCollection<string>();
        public int MaxLogLines { get; set; } = 500;
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
    public class FrpcManager : IDisposable
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _frpcDirectory;
        private readonly string _frpcExecutablePath;

        private readonly ConcurrentDictionary<int, FrpcProcessInfo> _tunnelProcesses = new();

        private readonly SemaphoreSlim _installSemaphore = new SemaphoreSlim(1, 1);

        // Windows Job Object 用于管理子进程生命周期
        private IntPtr _jobHandle;
        private bool _disposed = false;

        public string? InstalledVersion { get; private set; }

        public bool IsAnyProcessRunning => _tunnelProcesses.Values.Any(p => p.IsRunning);

        public IEnumerable<FrpcProcessInfo> RunningProcesses => _tunnelProcesses.Values.Where(p => p.IsRunning);

        public event EventHandler<FrpcProcessInfo>? TunnelProcessExited;
        public event EventHandler<FrpcProcessInfo>? TunnelProcessStarted;
        public event EventHandler<(int TunnelId, string LogLine)>? TunnelProcessLogAdded;

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

            // 初始化 Windows Job Object（仅在 Windows 上）
            InitializeJobObject();

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

        public bool StartTunnelProcess(int tunnelId, string tunnelName, string tunnelRemark, string? arguments = null)
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
                    WorkingDirectory = _frpcDirectory, // 显式设置工作目录
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                
                var info = new FrpcProcessInfo 
                { 
                    Process = process, 
                    TunnelId = tunnelId, 
                    TunnelName = tunnelName, 
                    TunnelRemark = tunnelRemark,
                    StartTime = DateTime.Now 
                };

                // 设置输出重定向事件
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        AddLog(info, e.Data);
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        AddLog(info, $"[ERROR] {e.Data}");
                    }
                };

                process.Exited += (s, e) => OnTunnelProcessExited(tunnelId);

                if (process.Start())
                {
                    // 将进程分配到 Job Object，使其随父进程退出
                    AssignProcessToJob(process);
                    
                    _tunnelProcesses[tunnelId] = info;
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    Log($"[PROCESS] Started tunnel {tunnelId} ({tunnelName}). PID: {process.Id}");
                    TunnelProcessStarted?.Invoke(this, info);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to start frpc process: {ex.Message}", ex);
            }
        }

        private void AddLog(FrpcProcessInfo info, string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logLine = $"[{timestamp}] {message}";
            
            // 保存到进程信息的日志集合中
            info.LogOutput.Add(logLine);
            
            // 限制日志行数为500行
            while (info.LogOutput.Count > info.MaxLogLines)
            {
                info.LogOutput.RemoveAt(0);
            }
            
            // 通过事件通知 UI 层添加日志
            TunnelProcessLogAdded?.Invoke(this, (info.TunnelId, logLine));
        }

        public bool StopTunnelProcess(int tunnelId)
        {
            if (_tunnelProcesses.TryRemove(tunnelId, out var info))
            {
                info.MarkAsExited();
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
                finally { try { info.Process.Dispose(); } catch { } }
            }
            return true;
        }

        public bool RestartTunnelProcess(int tunnelId)
        {
            if (_tunnelProcesses.TryGetValue(tunnelId, out var info))
            {
                var tunnelName = info.TunnelName;
                var file = info.Process.StartInfo.FileName;
                var arguments = info.Process.StartInfo.Arguments;
                
                // 停止进程
                StopTunnelProcess(tunnelId);
                
                // 重新启动
                return StartTunnelProcess(tunnelId, tunnelName, file, arguments);
            }
            return false;
        }

        public void StopAllTunnelProcesses()
        {
            var ids = _tunnelProcesses.Keys.ToList();
            foreach (var id in ids)
            {
                StopTunnelProcess(id);
            }
        }

        public FrpcProcessInfo? GetProcessInfo(int tunnelId)
        {
            _tunnelProcesses.TryGetValue(tunnelId, out var info);
            return info;
        }

        public IReadOnlyList<FrpcProcessInfo> GetAllProcesses()
        {
            return _tunnelProcesses.Values.ToList();
        }

        #endregion

        #region Events and Helpers

        private void OnTunnelProcessExited(int tunnelId)
        {
            if (_tunnelProcesses.TryGetValue(tunnelId, out var info))
            {
                info.MarkAsExited();
                Log($"[EVENT] Tunnel {tunnelId} exited.");
                TunnelProcessExited?.Invoke(this, info);
                
                // 从字典中移除
                _tunnelProcesses.TryRemove(tunnelId, out _);
                
                // 延迟释放进程对象
                try { info.Process.Dispose(); } catch { }
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

        #region Job Object Management

        private void InitializeJobObject()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log("[JOB] Job Object only supported on Windows");
                return;
            }

            try
            {
                // 创建 Job Object
                _jobHandle = JobObjectApi.CreateJobObject(IntPtr.Zero, null);
                if (_jobHandle == IntPtr.Zero)
                {
                    Log("[JOB] Failed to create Job Object");
                    return;
                }

                // 设置 Job Object 限制：当父进程退出时终止所有子进程
                var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = 0x2000 // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                };

                if (!JobObjectApi.SetInformationJobObject(_jobHandle, JOBOBJECTINFOCLASS.BasicLimitInformation, ref info, (uint)Marshal.SizeOf(typeof(JOBOBJECT_BASIC_LIMIT_INFORMATION))))
                {
                    Log("[JOB] Failed to set Job Object limits");
                }
                else
                {
                    Log("[JOB] Job Object initialized successfully");
                }
            }
            catch (Exception ex)
            {
                Log($"[JOB] Error initializing Job Object: {ex.Message}");
            }
        }

        private void AssignProcessToJob(Process process)
        {
            if (_jobHandle == IntPtr.Zero) return;

            try
            {
                if (!JobObjectApi.AssignProcessToJobObject(_jobHandle, process.Handle))
                {
                    Log("[JOB] Failed to assign process to Job Object");
                }
                else
                {
                    Log("[JOB] Process assigned to Job Object");
                }
            }
            catch (Exception ex)
            {
                Log($"[JOB] Error assigning process to Job Object: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 停止所有进程
            StopAllTunnelProcesses();

            // 关闭 Job Object 句柄（这会自动终止所有关联的子进程）
            if (_jobHandle != IntPtr.Zero)
            {
                try
                {
                    JobObjectApi.CloseHandle(_jobHandle);
                    Log("[JOB] Job Object handle closed");
                }
                catch { }
                _jobHandle = IntPtr.Zero;
            }

            _installSemaphore.Dispose();
        }

        #endregion
    }
}

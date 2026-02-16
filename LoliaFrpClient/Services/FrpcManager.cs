using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
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
        public string ConfigPath { get; set; } = string.Empty;
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
    /// Frpc 管理器，用于管理 frpc 的下载、安装、卸载、更新、检查就绪、启动进程和监测生命周期
    /// </summary>
    public class FrpcManager
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _frpcDirectory;
        private readonly string _frpcExecutablePath;
        private FrpcProcessInfo? _currentProcess;
        private readonly object _processLock = new object();

        /// <summary>
        /// 当前安装的版本
        /// </summary>
        public string? InstalledVersion { get; private set; }

        /// <summary>
        /// 当前进程是否正在运行
        /// </summary>
        public bool IsProcessRunning
        {
            get
            {
                lock (_processLock)
                {
                    return _currentProcess?.IsRunning ?? false;
                }
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="frpcDirectory">frpc 安装目录，如果为 null 则使用默认目录</param>
        public FrpcManager(string? frpcDirectory = null)
        {
            // 默认使用 ApplicationData 下的 LoliaFrpClient 目录
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _frpcDirectory = frpcDirectory ?? Path.Combine(appDataPath, "LoliaFrpClient", "frpc");
            
            // 确保目录存在
            Directory.CreateDirectory(_frpcDirectory);
            
            // 设置可执行文件路径
            var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
            var executableName = isWindows ? "frpc.exe" : "frpc";
            _frpcExecutablePath = Path.Combine(_frpcDirectory, executableName);
            
            // 加载已安装的版本信息
            LoadInstalledVersion();
        }

        /// <summary>
        /// 加载已安装的版本信息
        /// </summary>
        private void LoadInstalledVersion()
        {
            var versionFile = Path.Combine(_frpcDirectory, "version.txt");
            if (File.Exists(versionFile))
            {
                InstalledVersion = File.ReadAllText(versionFile).Trim();
            }
        }

        /// <summary>
        /// 保存已安装的版本信息
        /// </summary>
        private void SaveInstalledVersion(string version)
        {
            var versionFile = Path.Combine(_frpcDirectory, "version.txt");
            File.WriteAllText(versionFile, version);
            InstalledVersion = version;
        }

        /// <summary>
        /// 检查 frpc 是否已就绪
        /// </summary>
        /// <returns>是否就绪</returns>
        public bool IsFrpcReady()
        {
            return File.Exists(_frpcExecutablePath);
        }

        /// <summary>
        /// 获取安装状态
        /// </summary>
        /// <param name="latestVersion">最新版本号</param>
        /// <returns>安装状态</returns>
        public FrpcInstallStatus GetInstallStatus(string? latestVersion = null)
        {
            if (!IsFrpcReady())
            {
                return FrpcInstallStatus.NotInstalled;
            }

            if (!string.IsNullOrEmpty(latestVersion) && !string.IsNullOrEmpty(InstalledVersion))
            {
                if (latestVersion != InstalledVersion)
                {
                    return FrpcInstallStatus.Outdated;
                }
            }

            return FrpcInstallStatus.Installed;
        }

        /// <summary>
        /// 下载 frpc
        /// </summary>
        /// <param name="downloadUrl">下载 URL</param>
        /// <param name="progress">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>下载的文件路径</returns>
        public async Task<string> DownloadFrpcAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var tempDirectory = Path.Combine(_frpcDirectory, "temp");
            Directory.CreateDirectory(tempDirectory);

            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            var downloadPath = Path.Combine(tempDirectory, fileName);

            var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var totalBytesRead = 0L;

            using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                var bytesRead = 0;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0 && progress != null)
                    {
                        progress.Report((double)totalBytesRead / totalBytes);
                    }
                }
            }

            return downloadPath;
        }

        /// <summary>
        /// 安装 frpc
        /// </summary>
        /// <param name="downloadPath">下载的文件路径</param>
        /// <param name="version">版本号</param>
        /// <returns>是否成功</returns>
        public async Task<bool> InstallFrpcAsync(string downloadPath, string version)
        {
            try
            {
                // 解压文件
                var extractPath = Path.Combine(_frpcDirectory, "temp", "extract");
                Directory.CreateDirectory(extractPath);

                if (downloadPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ZipFile.ExtractToDirectory(downloadPath, extractPath);
                }
                else if (downloadPath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                {
                    // 处理 tar.gz 文件
                    using (var stream = File.OpenRead(downloadPath))
                    using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
                    using (var tar = new TarInputStream(gzip))
                    {
                        tar.ExtractContents(extractPath);
                    }
                }

                // 查找 frpc 可执行文件
                var executableName = Path.GetFileName(_frpcExecutablePath);
                var foundFile = Directory.GetFiles(extractPath, executableName, SearchOption.AllDirectories).FirstOrDefault();

                if (foundFile == null)
                {
                    throw new Exception("在下载的文件中找不到 frpc 可执行文件");
                }

                // 复制到目标位置
                if (File.Exists(_frpcExecutablePath))
                {
                    File.Delete(_frpcExecutablePath);
                }

                File.Copy(foundFile, _frpcExecutablePath);

                // 保存版本信息
                SaveInstalledVersion(version);

                // 清理临时文件
                try
                {
                    Directory.Delete(Path.Combine(_frpcDirectory, "temp"), true);
                }
                catch
                {
                    // 忽略清理错误
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"安装 frpc 失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 卸载 frpc
        /// </summary>
        /// <returns>是否成功</returns>
        public bool UninstallFrpc()
        {
            try
            {
                // 停止正在运行的进程
                StopFrpcProcess();

                // 删除可执行文件
                if (File.Exists(_frpcExecutablePath))
                {
                    File.Delete(_frpcExecutablePath);
                }

                // 删除版本文件
                var versionFile = Path.Combine(_frpcDirectory, "version.txt");
                if (File.Exists(versionFile))
                {
                    File.Delete(versionFile);
                }

                InstalledVersion = null;

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"卸载 frpc 失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 更新 frpc
        /// </summary>
        /// <param name="downloadUrl">下载 URL</param>
        /// <param name="version">新版本号</param>
        /// <param name="progress">进度回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功</returns>
        public async Task<bool> UpdateFrpcAsync(string downloadUrl, string version, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // 先停止正在运行的进程
            StopFrpcProcess();

            // 下载并安装新版本
            var downloadPath = await DownloadFrpcAsync(downloadUrl, progress, cancellationToken);
            return await InstallFrpcAsync(downloadPath, version);
        }

        /// <summary>
        /// 启动 frpc 进程
        /// </summary>
        /// <param name="configPath">配置文件路径</param>
        /// <param name="arguments">额外的命令行参数</param>
        /// <returns>是否成功</returns>
        public bool StartFrpcProcess(string configPath, string? arguments = null)
        {
            lock (_processLock)
            {
                if (_currentProcess?.IsRunning == true)
                {
                    throw new Exception("frpc 进程已在运行中");
                }

                if (!IsFrpcReady())
                {
                    throw new Exception("frpc 未安装");
                }

                if (!File.Exists(configPath))
                {
                    throw new Exception($"配置文件不存在: {configPath}");
                }

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _frpcExecutablePath,
                        Arguments = $"-c \"{configPath}\" {arguments ?? string.Empty}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    var process = new Process { StartInfo = startInfo };
                    process.Start();

                    _currentProcess = new FrpcProcessInfo
                    {
                        Process = process,
                        ConfigPath = configPath,
                        StartTime = DateTime.Now
                    };

                    // 监控进程退出
                    process.EnableRaisingEvents = true;
                    process.Exited += OnFrpcProcessExited;

                    return true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"启动 frpc 进程失败: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 停止 frpc 进程
        /// </summary>
        /// <returns>是否成功</returns>
        public bool StopFrpcProcess()
        {
            lock (_processLock)
            {
                if (_currentProcess == null || _currentProcess.Process.HasExited)
                {
                    return true;
                }

                try
                {
                    _currentProcess.Process.Kill();
                    _currentProcess.Process.WaitForExit(5000);
                    _currentProcess = null;
                    return true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"停止 frpc 进程失败: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 获取当前进程信息
        /// </summary>
        /// <returns>进程信息，如果没有运行则返回 null</returns>
        public FrpcProcessInfo? GetCurrentProcessInfo()
        {
            lock (_processLock)
            {
                if (_currentProcess?.IsRunning == true)
                {
                    return _currentProcess;
                }
                return null;
            }
        }

        /// <summary>
        /// frpc 进程退出事件
        /// </summary>
        public event EventHandler<FrpcProcessInfo>? FrpcProcessExited;

        /// <summary>
        /// frpc 进程退出处理
        /// </summary>
        private void OnFrpcProcessExited(object? sender, EventArgs e)
        {
            lock (_processLock)
            {
                if (_currentProcess != null)
                {
                    var processInfo = _currentProcess;
                    _currentProcess = null;
                    FrpcProcessExited?.Invoke(this, processInfo);
                }
            }
        }

        /// <summary>
        /// 获取 frpc 可执行文件路径
        /// </summary>
        /// <returns>可执行文件路径</returns>
        public string GetFrpcExecutablePath()
        {
            return _frpcExecutablePath;
        }

        /// <summary>
        /// 获取 frpc 安装目录
        /// </summary>
        /// <returns>安装目录</returns>
        public string GetFrpcDirectory()
        {
            return _frpcDirectory;
        }
    }

    /// <summary>
    /// Tar 输入流（用于解压 tar.gz 文件）
    /// </summary>
    internal class TarInputStream : IDisposable
    {
        private readonly Stream _stream;
        private bool _disposed;

        public TarInputStream(Stream stream)
        {
            _stream = stream;
        }

        public void ExtractContents(string destinationPath)
        {
            // 简化实现，实际项目中应该使用 SharpCompress 等库
            // 这里只是占位符
            throw new NotImplementedException("tar.gz 解压需要使用第三方库，如 SharpCompress");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stream.Dispose();
                _disposed = true;
            }
        }
    }
}

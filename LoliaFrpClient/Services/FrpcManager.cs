using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using LoliaFrpClient.Utilities;

namespace LoliaFrpClient.Services;

public enum FrpcInstallStatus { NotInstalled, Installed, Outdated }

public record FrpcProcessInfo(int TunnelId, string TunnelName, string? TunnelRemark, Process Process)
{
    public ObservableCollection<string> Logs { get; } = [];
    public DateTime StartTime { get; init; } = DateTime.Now;
    public bool IsRunning { get; private set; } = true;
    public IEnumerable<string> LogOutput => Logs;

    public void AddLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Logs.Add(line);
        if (Logs.Count > 500) Logs.RemoveAt(0);
    }

    public void MarkAsExited() => IsRunning = false;
}

public partial class FrpcManager : IDisposable
{
    public event EventHandler<FrpcProcessInfo>? TunnelProcessStarted;
    public event EventHandler<FrpcProcessInfo>? TunnelProcessExited;
    public event EventHandler<(int TunnelId, string LogLine)>? TunnelProcessLogAdded;

    private readonly string _workDir;
    private readonly string _binPath;
    private readonly HttpClient _http = new();
    private readonly ConcurrentDictionary<int, FrpcProcessInfo> _processes = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private nint _jobHandle;

    public string? InstalledVersion { get; private set; }
    public bool IsAnyRunning => _processes.Values.Any(p => p.IsRunning);
    public bool IsAnyProcessRunning => IsAnyRunning;

    public FrpcManager(string? path = null)
    {
        _workDir = path ?? Path.Combine(GetAppDataPath(), "frpc");
        Directory.CreateDirectory(_workDir);
        
        _binPath = Path.Combine(_workDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "frpc.exe" : "frpc");
        
        InitJobObject();
        LoadVersion();
        
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    private string GetAppDataPath()
    {
        try { return ApplicationData.Current.LocalFolder.Path; }
        catch { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LoliaFrpClient"); }
    }

    #region Installation

    public async Task<bool> InstallAsync(string url, string version, IProgress<double>? progress = null)
    {
        await _lock.WaitAsync();
        try
        {
            var tempFile = Path.Combine(_workDir, "download.tmp");
            using (var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? -1L;
                await using var fs = new FileStream(tempFile, FileMode.Create);
                await using var stream = await resp.Content.ReadAsStreamAsync();
                
                var buffer = new byte[8192];
                long read = 0;
                int n;
                while ((n = await stream.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, n));
                    read += n;
                    if (total > 0) progress?.Report((double)read / total);
                }
            }

            var extractDir = Path.Combine(_workDir, "extract_temp");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);

            if (url.EndsWith(".zip")) ZipFile.ExtractToDirectory(tempFile, extractDir);
            else
            {
                await using var fs = File.OpenRead(tempFile);
                await using var gzip = new GZipStream(fs, CompressionMode.Decompress);
                await TarFile.ExtractToDirectoryAsync(gzip, extractDir, true);
            }

            var exeName = Path.GetFileName(_binPath);
            var sourceExe = Directory.GetFiles(extractDir, exeName, SearchOption.AllDirectories).FirstOrDefault() 
                ?? throw new FileNotFoundException("Binary not found in package");

            StopAll();
            if (File.Exists(_binPath)) File.Delete(_binPath);
            File.Move(sourceExe, _binPath);
            
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start("chmod", $"+x {_binPath}")?.WaitForExit();

            File.WriteAllText(Path.Combine(_workDir, "version.txt"), version);
            InstalledVersion = version;
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public FrpcInstallStatus GetInstallStatus(string? latestVersion)
    {
        if (string.IsNullOrEmpty(InstalledVersion)) return FrpcInstallStatus.NotInstalled;
        if (latestVersion == null || InstalledVersion == latestVersion) return FrpcInstallStatus.Installed;
        return FrpcInstallStatus.Outdated;
    }

    public void UninstallFrpc()
    {
        StopAll();
        if (File.Exists(_binPath)) File.Delete(_binPath);
        var versionFile = Path.Combine(_workDir, "version.txt");
        if (File.Exists(versionFile)) File.Delete(versionFile);
        InstalledVersion = null;
    }

    #endregion

    #region Control

    public void Start(int id, string name, string args, string? remark = null)
    {
        if (_processes.TryGetValue(id, out var p) && p.IsRunning) return;

        var startInfo = new ProcessStartInfo(_binPath, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _workDir,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var proc = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var info = new FrpcProcessInfo(id, name, remark, proc);

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                info.AddLog(e.Data);
                TunnelProcessLogAdded?.Invoke(this, (id, e.Data));
            }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                info.AddLog($"[ERR] {e.Data}");
                TunnelProcessLogAdded?.Invoke(this, (id, $"[ERR] {e.Data}"));
            }
        };
        proc.Exited += (_, _) =>
        {
            info.MarkAsExited();
            _processes.TryRemove(id, out var removed);
            TunnelProcessExited?.Invoke(this, info);
        };

        proc.Start();
        AssignToJob(proc);
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        
        _processes[id] = info;
        TunnelProcessStarted?.Invoke(this, info);
    }

    public void Stop(int id)
    {
        if (!_processes.TryRemove(id, out var info)) return;
        var wasRunning = info.IsRunning;
        info.MarkAsExited();
        try
        {
            if (wasRunning) info.Process.Kill(true);
        }
        catch (Exception ex)
        {
            info.AddLog($"Stop Error: {ex.Message}");
        }
        finally
        {
            info.Process.Dispose();
        }
    }

    public void StopAll() => _processes.Keys.ToList().ForEach(Stop);

    public bool IsTunnelProcessRunning(int tunnelId) => _processes.ContainsKey(tunnelId) && _processes[tunnelId].IsRunning;

    public FrpcProcessInfo? GetProcessInfo(int tunnelId) =>_processes.TryGetValue(tunnelId, out var info) ? info : null;

    public IReadOnlyCollection<FrpcProcessInfo> GetAllProcesses() => _processes.Values.ToList();

    public void RestartTunnelProcess(int tunnelId)
    {
        if (!_processes.TryGetValue(tunnelId, out var info)) return;
        var args = info.Process.StartInfo.Arguments;
        Stop(tunnelId);
        Start(tunnelId, info.TunnelName, info.TunnelRemark, args);
    }

    #endregion

    private void InitJobObject()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        _jobHandle = JobObjectApi.CreateJobObject(IntPtr.Zero, null);
    
        var limits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = 0x2000 | 0x0800 
            }
        };

        bool success = JobObjectApi.SetInformationJobObject(
            _jobHandle, 
            JOBOBJECTINFOCLASS.ExtendedLimitInformation, 
            ref limits, 
            (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>());
    }

    private void AssignToJob(Process p)
    {
        if (_jobHandle != nint.Zero) JobObjectApi.AssignProcessToJobObject(_jobHandle, p.Handle);
    }
    private void LoadVersion()
    {
        var vFile = Path.Combine(_workDir, "version.txt");
        if (File.Exists(vFile)) InstalledVersion = File.ReadAllText(vFile).Trim();
    }
    public void Dispose()
    {
        StopAll();
        if (_jobHandle != nint.Zero) JobObjectApi.CloseHandle(_jobHandle);
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
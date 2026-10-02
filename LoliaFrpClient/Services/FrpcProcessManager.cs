using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using LoliaFrpClient.Core.Frpc;

namespace LoliaFrpClient.Services;

public enum FrpcTunnelState
{
    Offline,
    Starting,
    Online,

    // Server reports the tunnel up but we hold no process for it: someone else is using it.
    // Starting it here would kick them off, since frpc logins for one tunnel are mutually exclusive.
    OnlineRemote,

    Error
}

internal sealed record FrpcStartResult(bool Success, string Message)
{
    public static FrpcStartResult Ok()
    {
        return new FrpcStartResult(true, string.Empty);
    }

    public static FrpcStartResult Fail(string message)
    {
        return new FrpcStartResult(false, message);
    }
}

// Only tunnels started through TryStart are owned here and eligible for Stop. Callers must
// gate their UI on that, or a tunnel running elsewhere gets kicked offline.
//
// Lock discipline: _gate guards the dictionaries only. Kill and WaitForExit always run
// outside it, because a stuck process would otherwise block the UI thread for seconds.
internal sealed class FrpcProcessManager : IDisposable
{
    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(3);

    private const string StartedKeyword = "成功启动隧道";

    private const string LoggedInKeyword = "登录服务器成功";

    private const string ConnectingKeyword = "尝试连接到服务器";

    private const int MaxRetainedLogs = 12;

    private static readonly object SingletonGate = new();
    private static FrpcProcessManager? _current;

    private readonly object _gate = new();
    private readonly Dictionary<string, FrpcSession> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FrpcLogBuffer> _logs = new(StringComparer.Ordinal);
    private bool _disposed;

    public FrpcProcessManager()
    {
        // Without this, a normal exit leaves frpc children behind holding their tunnels.
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    public static FrpcProcessManager Current
    {
        get
        {
            lock (SingletonGate)
            {
                return _current ??= new FrpcProcessManager();
            }
        }
    }

    // May fire on any thread; subscribers must marshal to the UI thread themselves.
    public event Action<string>? StateChanged;

    public bool IsLocal(string tunnelName)
    {
        lock (_gate)
        {
            return _sessions.ContainsKey(tunnelName);
        }
    }

    public FrpcTunnelState GetLocalState(string tunnelName)
    {
        lock (_gate)
        {
            return _sessions.TryGetValue(tunnelName, out var session)
                ? session.State
                : FrpcTunnelState.Offline;
        }
    }

    public Dictionary<string, FrpcTunnelState> SnapshotLocalStates()
    {
        lock (_gate)
        {
            var snapshot = new Dictionary<string, FrpcTunnelState>(_sessions.Count, StringComparer.Ordinal);
            foreach (var (name, session) in _sessions) snapshot[name] = session.State;

            return snapshot;
        }
    }

    // Kept per tunnel and outliving the session so the log of a crashed tunnel stays readable.
    public FrpcLogBuffer GetLog(string tunnelName)
    {
        lock (_gate)
        {
            if (_logs.TryGetValue(tunnelName, out var existing)) return existing;

            if (_logs.Count >= MaxRetainedLogs)
                foreach (var key in new List<string>(_logs.Keys))
                    if (!_sessions.ContainsKey(key))
                    {
                        _logs.Remove(key);
                        break;
                    }

            var buffer = new FrpcLogBuffer();
            _logs[tunnelName] = buffer;
            return buffer;
        }
    }

    public FrpcStartResult TryStart(string tunnelName, int tunnelId, string token, string frpcPath)
    {
        if (string.IsNullOrWhiteSpace(tunnelName)) return FrpcStartResult.Fail("隧道名无效");

        if (string.IsNullOrWhiteSpace(frpcPath) || !File.Exists(frpcPath))
            return FrpcStartResult.Fail("frpc 路径无效,请先在设置中指定或下载");

        if (tunnelId <= 0) return FrpcStartResult.Fail("隧道 ID 无效");

        if (string.IsNullOrWhiteSpace(token)) return FrpcStartResult.Fail("隧道 token 为空");

        var process = new Process
        {
            // Belongs to Process, not ProcessStartInfo; without it Exited never fires.
            EnableRaisingEvents = true,
            StartInfo = new ProcessStartInfo
            {
                FileName = frpcPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,

                // Empty means "inherit the current directory", which is what desktop wants.
                // Android must override it: the app's cwd is / and the root is read-only, so
                // frpc's ./autotls-cache write fails there. See FrpcLocator.WorkingDirectory.
                WorkingDirectory = FrpcLocator.WorkingDirectory ?? string.Empty,

                // Explicit UTF-8: frpc prints Chinese, and the console code page would mangle it.
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        // ArgumentList rather than a concatenated string, so a token containing spaces or
        // quotes cannot break the argument apart.
        process.StartInfo.ArgumentList.Add("-t");
        process.StartInfo.ArgumentList.Add($"{tunnelId}:{token}");

        var session = new FrpcSession(tunnelName, process);

        process.OutputDataReceived += (_, e) => OnOutput(session, e.Data, false);
        process.ErrorDataReceived += (_, e) => OnOutput(session, e.Data, true);
        process.Exited += (_, _) => OnExited(session);

        string? rejection = null;
        lock (_gate)
        {
            if (_disposed)
                rejection = "进程管理器已释放";
            else if (_sessions.ContainsKey(tunnelName))
                rejection = "该隧道已在本客户端运行中";
            else
                // Register before starting: a process that exits immediately would otherwise
                // raise Exited before it was recorded, leaving a dead session in the table.
                _sessions[tunnelName] = session;
        }

        if (rejection is not null)
        {
            session.Dispose();
            return FrpcStartResult.Fail(rejection);
        }

        try
        {
            if (!process.Start())
            {
                Forget(tunnelName, session);
                return FrpcStartResult.Fail("frpc 启动失败");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            Forget(tunnelName, session);
            return FrpcStartResult.Fail($"frpc 启动失败: {ex.Message}");
        }

        // Only the id, never the token: the command line carries the credential.
        GetLog(tunnelName).Append($"已启动 (PID {process.Id}, 参数 -t {tunnelId}:***)");
        RaiseStateChanged(tunnelName);
        SyncKeepAlive();
        return FrpcStartResult.Ok();
    }

    // Returns false when this client holds no process for the tunnel, in which case the
    // caller must refuse the operation.
    public bool Stop(string tunnelName)
    {
        FrpcSession? session;
        lock (_gate)
        {
            if (!_sessions.Remove(tunnelName, out session)) return false;

            session.StopRequested = true;
        }

        Terminate(session);
        GetLog(tunnelName).Append("已停止");
        SyncKeepAlive();
        return true;
    }

    public int StopAll()
    {
        List<string> names;
        lock (_gate)
        {
            names = new List<string>(_sessions.Keys);
        }

        var stopped = 0;
        foreach (var name in names)
            if (Stop(name))
                stopped++;

        return stopped;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;

            _disposed = true;
        }

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        StopAll();
    }

    private void Forget(string tunnelName, FrpcSession session)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(tunnelName, out var current) && ReferenceEquals(current, session))
                _sessions.Remove(tunnelName);
        }

        session.Dispose();

        SyncKeepAlive();
    }

    private void OnOutput(FrpcSession session, string? line, bool isError)
    {
        if (string.IsNullOrEmpty(line)) return;

        GetLog(session.TunnelName).Append(line);
        UpdateStateFromOutput(session, line);
    }

    // A live process does not mean a usable tunnel: frpc can be up and stuck reconnecting,
    // so state is driven only by its own log lines. The bracketed value in each line is a
    // run id, not the tunnel name; ownership comes from which process emitted it.
    private void UpdateStateFromOutput(FrpcSession session, string line)
    {
        FrpcTunnelState next;

        if (line.Contains(StartedKeyword, StringComparison.Ordinal) ||
            line.Contains(LoggedInKeyword, StringComparison.Ordinal))
            next = FrpcTunnelState.Online;
        else if (line.Contains(ConnectingKeyword, StringComparison.Ordinal))
            next = FrpcTunnelState.Starting;
        else
            return;

        lock (_gate)
        {
            if (session.State == next) return;

            session.State = next;
        }

        RaiseStateChanged(session.TunnelName);
    }

    private void OnExited(FrpcSession session)
    {
        bool owned;
        lock (_gate)
        {
            // Only remove when the recorded session is still this same instance. Stop may have
            // already replaced it, and deleting by name would then wipe the new process's entry.
            owned = _sessions.TryGetValue(session.TunnelName, out var current)
                    && ReferenceEquals(current, session)
                    && _sessions.Remove(session.TunnelName);
        }

        if (!owned)
        {
            session.Dispose();
            return;
        }

        var exitCode = TryGetExitCode(session.Process);

        if (session.StopRequested)
            GetLog(session.TunnelName).Append("进程已退出");
        else
            GetLog(session.TunnelName).Append($"frpc 意外退出 (exit code {exitCode?.ToString() ?? "未知"})");

        session.Dispose();
        RaiseStateChanged(session.TunnelName);
        SyncKeepAlive();
    }

    private void SyncKeepAlive()
    {
        int running;
        lock (_gate)
        {
            running = _sessions.Count;
        }

        TunnelKeepAlive.SetActive(running > 0, running);
    }

    private void Terminate(FrpcSession session)
    {
        try
        {
            if (!session.Process.HasExited)
            {
                // The whole tree: frpc may spawn helper processes.
                session.Process.Kill(true);
                session.Process.WaitForExit((int)KillTimeout.TotalMilliseconds);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            GetLog(session.TunnelName).Append($"结束进程失败: {ex.Message}");
        }
        finally
        {
            session.Dispose();
        }
    }

    private static int? TryGetExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }

    private void RaiseStateChanged(string tunnelName)
    {
        try
        {
            StateChanged?.Invoke(tunnelName);
        }
        catch (Exception)
        {
            // A misbehaving subscriber must not break process management, and must not
            // propagate onto the stdout callback thread.
        }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        StopAll();
    }

    // Mutable fields are read and written only under _gate.
    private sealed class FrpcSession(string tunnelName, Process process) : IDisposable
    {
        public string TunnelName { get; } = tunnelName;

        public Process Process { get; } = process;

        public FrpcTunnelState State { get; set; } = FrpcTunnelState.Starting;

        // Distinguishes a requested stop from an unexpected exit.
        public bool StopRequested { get; set; }

        public void Dispose()
        {
            Process.Dispose();
        }
    }
}
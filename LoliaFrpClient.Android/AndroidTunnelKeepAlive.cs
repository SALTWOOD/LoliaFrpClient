using System;
using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Provider;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.Android;

internal sealed class AndroidTunnelKeepAlive : ITunnelKeepAliveHost
{
    private const string LockTag = "LoliaFrp:tunnel";

    private readonly object _gate = new();

    private readonly Context _context;
    private readonly PowerManager? _power;
    private readonly WifiManager? _wifi;

    private bool _locksRequested;

    private bool _active;
    private PowerManager.WakeLock? _cpuLock;
    private WifiManager.WifiLock? _wifiLock;

    internal AndroidTunnelKeepAlive(Context context)
    {
        _context = context;
        _power = context.GetSystemService(Context.PowerService) as PowerManager;
        _wifi = context.GetSystemService(Context.WifiService) as WifiManager;
    }

    public void SetActive(bool active, int tunnelCount)
    {
        lock (_gate)
        {
            if (!active) _locksRequested = false;

            _active = active;

            if (active) StartService(tunnelCount);
            else StopService();

            ApplyLocks();
        }
    }

    public bool ArePowerLocksHeld
    {
        get
        {
            lock (_gate)
            {
                return _cpuLock is { IsHeld: true };
            }
        }
    }

    public bool TogglePowerLocks()
    {
        lock (_gate)
        {
            _locksRequested = _cpuLock is not { IsHeld: true };

            ApplyLocks();

            return _cpuLock is { IsHeld: true };
        }
    }

    public bool IsBatteryOptimizationIgnored()
    {
        return _power?.IsIgnoringBatteryOptimizations(_context.PackageName!) ?? true;
    }

    public void RequestBatteryOptimizationExemption()
    {
        try
        {
            var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations);
            intent.SetData(global::Android.Net.Uri.Parse($"package:{_context.PackageName}"));

            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
        }
        catch (ActivityNotFoundException)
        {
            OpenAppDetails();
        }
    }

    private void OpenAppDetails()
    {
        var intent = new Intent(Settings.ActionApplicationDetailsSettings);
        intent.SetData(global::Android.Net.Uri.Parse($"package:{_context.PackageName}"));
        intent.AddFlags(ActivityFlags.NewTask);
        _context.StartActivity(intent);
    }

    private void StartService(int tunnelCount)
    {
        var intent = new Intent(_context, typeof(TunnelKeepAliveService));
        intent.PutExtra(TunnelKeepAliveService.ExtraTunnelCount, tunnelCount);

        if (OperatingSystem.IsAndroidVersionAtLeast(26)) _context.StartForegroundService(intent);
        else _context.StartService(intent);
    }

    private void StopService()
    {
        _context.StopService(new Intent(_context, typeof(TunnelKeepAliveService)));
    }

    private void ApplyLocks()
    {
        ApplyCpuLock(_active && _locksRequested);
        ApplyWifiLock(_active && _locksRequested);
    }

    private void ApplyCpuLock(bool wanted)
    {
        if (wanted)
        {
            if (_power is null) return;

            if (_cpuLock is null)
            {
                var wakeLock = _power.NewWakeLock(WakeLockFlags.Partial, LockTag)!;

                wakeLock.SetReferenceCounted(false);
                _cpuLock = wakeLock;
            }

            if (!_cpuLock.IsHeld) _cpuLock.Acquire();
            return;
        }

        if (_cpuLock is { IsHeld: true }) _cpuLock.Release();
    }

    private void ApplyWifiLock(bool wanted)
    {
        if (wanted)
        {
            if (_wifi is null) return;

            if (_wifiLock is null)
            {
                _wifiLock = _wifi.CreateWifiLock(WifiMode.FullHighPerf, LockTag);
                _wifiLock?.SetReferenceCounted(false);
            }

            if (_wifiLock is { IsHeld: false }) _wifiLock.Acquire();
            return;
        }

        if (_wifiLock is { IsHeld: true }) _wifiLock.Release();
    }
}

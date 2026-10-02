using System;
using System.Diagnostics;

namespace LoliaFrpClient.Services;

public interface ITunnelKeepAliveHost
{
    void SetActive(bool active, int tunnelCount);

    bool ArePowerLocksHeld { get; }

    bool TogglePowerLocks();

    bool IsBatteryOptimizationIgnored();

    void RequestBatteryOptimizationExemption();
}

public static class TunnelKeepAlive
{
    private static ITunnelKeepAliveHost? _host;

    public static bool IsSupported => _host is not null;

    public static void UseHost(ITunnelKeepAliveHost? host)
    {
        _host = host;
    }

    public static void SetActive(bool active, int tunnelCount)
    {
        Invoke(() => _host?.SetActive(active, tunnelCount));
    }

    public static bool ArePowerLocksHeld => _host?.ArePowerLocksHeld ?? false;

    public static bool TogglePowerLocks()
    {
        try
        {
            return _host?.TogglePowerLocks() ?? false;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[keep-alive] {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public static bool IsBatteryOptimizationIgnored()
    {
        try
        {
            return _host?.IsBatteryOptimizationIgnored() ?? true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void RequestBatteryOptimizationExemption()
    {
        Invoke(() => _host?.RequestBatteryOptimizationExemption());
    }

    public static int StopAllTunnels()
    {
        try
        {
            return FrpcProcessManager.Current.StopAll();
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static void Invoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[keep-alive] {ex.GetType().Name}: {ex.Message}");
        }
    }
}

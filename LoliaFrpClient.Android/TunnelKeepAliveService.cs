using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using LoliaFrpClient.Services;

namespace LoliaFrpClient.Android;

[Service(
    Name = "com.loliafrp.client.TunnelKeepAliveService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeSpecialUse)]
public sealed class TunnelKeepAliveService : Service
{
    internal const string ExtraTunnelCount = "tunnel_count";

    internal const string ActionToggleLocks = "com.loliafrp.client.TOGGLE_LOCKS";

    internal const string ActionExit = "com.loliafrp.client.EXIT";

    private int _tunnelCount = 1;

    public override void OnCreate()
    {
        base.OnCreate();
        KeepAliveNotifications.EnsureChannel(this);
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var action = intent?.Action;

        if (string.Equals(action, ActionExit, StringComparison.Ordinal))
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        if (string.Equals(action, ActionToggleLocks, StringComparison.Ordinal))
            TunnelKeepAlive.TogglePowerLocks();

        _tunnelCount = intent?.GetIntExtra(ExtraTunnelCount, _tunnelCount) ?? _tunnelCount;

        StartForeground(
            KeepAliveNotifications.Id,
            KeepAliveNotifications.Create(this, _tunnelCount, TunnelKeepAlive.ArePowerLocksHeld));

        return StartCommandResult.NotSticky;
    }

    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }

    public override void OnDestroy()
    {
        _ = Task.Run(TunnelKeepAlive.StopAllTunnels);

        base.OnDestroy();
    }
}

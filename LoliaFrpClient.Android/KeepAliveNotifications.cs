using System;
using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace LoliaFrpClient.Android;

internal static class KeepAliveNotifications
{
    private const string ChannelId = "tunnel-keepalive";

    private const int OpenRequest = 0;
    private const int ExitRequest = 1;
    private const int LockRequest = 2;

    internal static int Id { get; } = 0x4C46;

    internal static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        if (context.GetSystemService(Context.NotificationService) is not NotificationManager manager) return;

        var channel = new NotificationChannel(ChannelId, "隧道运行中", NotificationImportance.Low)
        {
            Description = "隧道运行期间常驻,避免系统在后台回收应用进程。"
        };
        channel.SetShowBadge(false);

        manager.CreateNotificationChannel(channel);
    }

    internal static Notification Create(Context context, int tunnelCount, bool locksHeld)
    {
        var open = new Intent(context, typeof(MainActivity));
        open.AddFlags(ActivityFlags.SingleTop);

        // 逐个 Set 而不是串成一条链:AndroidX 的绑定把每个 Set* 都标成可空返回,链式调用会
        // 一路报「解引用可能出现空引用」。
        var builder = new NotificationCompat.Builder(context, ChannelId);
        builder.SetContentTitle("LoliaFrp");
        builder.SetContentText(locksHeld
            ? $"{tunnelCount} 条隧道运行中 · 活跃锁已开启"
            : $"{tunnelCount} 条隧道运行中");
        builder.SetSmallIcon(Resource.Drawable.ic_stat_tunnel);
        builder.SetContentIntent(PendingIntent.GetActivity(
            context, OpenRequest, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent));
        builder.SetOngoing(true);
        builder.SetShowWhen(false);
        builder.SetOnlyAlertOnce(true);
        builder.SetPriority((int)NotificationPriority.Low);
        builder.SetCategory(Notification.CategoryService);
        builder.SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate);

        builder.AddAction(new NotificationCompat.Action(
            Resource.Drawable.ic_notif_exit,
            "退出",
            ServiceAction(context, ExitRequest, TunnelKeepAliveService.ActionExit)));

        // 活跃锁的授权放在这里而不是设置页:它是一次性的、只对本次运行有效,而「不用了」
        // 的入口本来就在通知上。
        builder.AddAction(new NotificationCompat.Action(
            Resource.Drawable.ic_notif_lock,
            locksHeld ? "释放活跃锁" : "获取活跃锁",
            ServiceAction(context, LockRequest, TunnelKeepAliveService.ActionToggleLocks)));

        return builder.Build()!;
    }

    // GetService, not GetForegroundService: these fire while the service is already running in
    // the foreground, and startService carries no obligation to call startForeground within 5
    // seconds — which the exit button would otherwise have to satisfy just to stop itself.
    private static PendingIntent? ServiceAction(Context context, int requestCode, string action)
    {
        var intent = new Intent(context, typeof(TunnelKeepAliveService));
        intent.SetAction(action);

        return PendingIntent.GetService(
            context, requestCode, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
    }
}

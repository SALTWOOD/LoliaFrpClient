using System;
using Sentry;

namespace LoliaFrpClient.Services;

public static partial class CrashReporting
{
    private static bool _started;

    public static void Start()
    {
        if (_started || Dsn.Length == 0 || !AppSettings.Current.CrashReporting) return;

        try
        {
            SentrySdk.Init(options =>
            {
                options.Dsn = Dsn;
                options.Release = UpdateChecker.CurrentVersionText;
                options.IsGlobalModeEnabled = true;
                options.AutoSessionTracking = false;
            });

            _started = true;
        } catch (Exception) { /* 不管 */ }
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled) Start();
        else Shutdown();
    }

    public static void Shutdown()
    {
        if (!_started) return;

        SentrySdk.Close();
        _started = false;
    }
}

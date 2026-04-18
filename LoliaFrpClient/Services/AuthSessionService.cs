using System;
using System.Threading;
using System.Threading.Tasks;

namespace LoliaFrpClient.Services;

public sealed class AuthSessionService
{
    private static readonly Lazy<AuthSessionService> _instance = new(() => new AuthSessionService());
    private int _isHandlingUnauthorized;

    private AuthSessionService()
    {
    }

    public static AuthSessionService Instance => _instance.Value;

    public event Func<Task>? UnauthorizedDetected;

    public async Task NotifyUnauthorizedAsync()
    {
        if (Interlocked.CompareExchange(ref _isHandlingUnauthorized, 1, 0) != 0)
        {
            return;
        }

        var handler = UnauthorizedDetected;
        if (handler == null)
        {
            Interlocked.Exchange(ref _isHandlingUnauthorized, 0);
            return;
        }

        try
        {
            await handler.Invoke();
        }
        catch
        {
            Interlocked.Exchange(ref _isHandlingUnauthorized, 0);
            throw;
        }
    }

    public void CompleteUnauthorizedHandling()
    {
        Interlocked.Exchange(ref _isHandlingUnauthorized, 0);
    }
}

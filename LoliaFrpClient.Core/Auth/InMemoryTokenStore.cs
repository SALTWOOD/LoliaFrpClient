namespace LoliaFrpClient.Core;

/// <summary>
///     纯内存的凭证存储。用于测试,或宿主应用自行管理持久化时使用。
/// </summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    private readonly object _gate = new();
    private string? _accessToken;
    private TokenOrigin _origin;
    private string? _refreshToken;

    /// <inheritdoc />
    public string? AccessToken
    {
        get
        {
            lock (_gate)
            {
                return _accessToken;
            }
        }
        set
        {
            lock (_gate)
            {
                _accessToken = value;
            }
        }
    }

    /// <inheritdoc />
    public string? RefreshToken
    {
        get
        {
            lock (_gate)
            {
                return _refreshToken;
            }
        }
        set
        {
            lock (_gate)
            {
                _refreshToken = value;
            }
        }
    }

    /// <inheritdoc />
    public TokenOrigin Origin
    {
        get
        {
            lock (_gate)
            {
                return _origin;
            }
        }
        set
        {
            lock (_gate)
            {
                _origin = value;
            }
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_gate)
        {
            _accessToken = null;
            _refreshToken = null;
            _origin = TokenOrigin.None;
        }
    }
}

namespace LoliaFrpClient.Core;

public sealed class InMemoryTokenStore : ITokenStore
{
    private readonly object _gate = new();
    private string? _accessToken;
    private TokenOrigin _origin;
    private string? _refreshToken;

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
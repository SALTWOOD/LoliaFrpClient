using LoliaFrpClient.Api;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace LoliaFrpClient.Core;

public sealed class ApiSession : IDisposable
{
    private static readonly object CurrentGate = new();
    private static ApiSession? _current;
    private readonly HttpClient _http;

    public ApiSession(ApiOptions? options = null, ITokenStore? tokens = null, HttpClient? httpClient = null)
    {
        Options = options ?? new ApiOptions();
        Tokens = tokens ?? new FileTokenStore();
        OAuth = new OAuthClient(Options.OAuth);

        if (httpClient is not null)
        {
            _http = httpClient;
        }
        else
        {
            var handlers = KiotaClientFactory.CreateDefaultHandlers();
            handlers.Add(new UnauthorizedHandler(Tokens, TryRefreshAsync, RaiseUnauthorized));
            _http = KiotaClientFactory.Create(handlers);
        }

        _http.Timeout = Options.Timeout;

        var adapter = new HttpClientRequestAdapter(new BearerTokenAuthenticationProvider(Tokens), httpClient: _http)
        {
            BaseUrl = Options.BaseUrl
        };

        Client = new ApiClient(adapter);
    }

    public static ApiSession Current
    {
        get
        {
            lock (CurrentGate)
            {
                return _current ??= new ApiSession();
            }
        }
    }

    public ApiClient Client { get; }

    public ITokenStore Tokens { get; }

    public ApiOptions Options { get; }

    public OAuthClient OAuth { get; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Tokens.AccessToken);

    public event Action? UnauthorizedDetected;

    public static void Initialize(ApiSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (CurrentGate)
        {
            _current = session;
        }
    }

    public static void Reset()
    {
        lock (CurrentGate)
        {
            _current?.Dispose();
            _current = null;
        }
    }

    public void SignOut()
    {
        Tokens.Clear();
    }

    public void Dispose()
    {
        _http.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        var refreshToken = Tokens.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken)) return false;

        try
        {
            var result = await OAuth.RefreshAsync(refreshToken, cancellationToken).ConfigureAwait(false);

            Tokens.AccessToken = result.AccessToken;
            if (!string.IsNullOrWhiteSpace(result.RefreshToken)) Tokens.RefreshToken = result.RefreshToken;

            Tokens.Origin = TokenOrigin.OAuth2;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void RaiseUnauthorized()
    {
        UnauthorizedDetected?.Invoke();
    }
}
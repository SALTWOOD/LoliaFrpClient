using LoliaFrpClient.Api;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace LoliaFrpClient.Core;

/// <summary>
///     一次会话的全部上下文:生成的 Kiota 客户端、凭证存储、OAuth 客户端,以及 401 的兜底通知。
///     <para>
///         门面类的无参构造函数从这里取客户端,因此业务代码可以写 <c>new User()</c> 而无需关心装配。
///         需要在测试或非默认配置下使用时,把会话显式传给门面的另一个构造函数即可。
///     </para>
/// </summary>
public sealed class ApiSession : IDisposable
{
    private static readonly object CurrentGate = new();
    private static ApiSession? _current;
    private readonly HttpClient _http;

    /// <summary>创建会话。</summary>
    /// <param name="options">端点配置。为 <c>null</c> 时使用默认的 <see cref="ApiOptions" />。</param>
    /// <param name="tokens">凭证存储。为 <c>null</c> 时使用默认路径的 <see cref="FileTokenStore" />。</param>
    /// <param name="httpClient">可选的底层 HttpClient,便于测试注入。</param>
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

    /// <summary>
    ///     当前会话。首次访问时按默认配置创建,之后可用 <see cref="Initialize" /> 替换。
    /// </summary>
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

    /// <summary>生成的 Kiota 客户端。门面类只通过它发请求。</summary>
    public ApiClient Client { get; }

    /// <summary>凭证存储。</summary>
    public ITokenStore Tokens { get; }

    /// <summary>端点配置。</summary>
    public ApiOptions Options { get; }

    /// <summary>OAuth 客户端。</summary>
    public OAuthClient OAuth { get; }

    /// <summary>当前是否持有访问令牌。</summary>
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Tokens.AccessToken);

    /// <summary>
    ///     令牌刷新失败、需要重新登录时触发。宿主应用应在此跳转登录页。
    /// </summary>
    public event Action? UnauthorizedDetected;

    /// <summary>设置当前会话,替换掉懒加载的默认实例。</summary>
    public static void Initialize(ApiSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (CurrentGate)
        {
            _current = session;
        }
    }

    /// <summary>清除当前会话(测试用)。</summary>
    public static void Reset()
    {
        lock (CurrentGate)
        {
            _current?.Dispose();
            _current = null;
        }
    }

    /// <summary>登出:清空凭证。不发请求,服务端会话由 <c>Auth.LogoutAsync</c> 负责撤销。</summary>
    public void SignOut() => Tokens.Clear();

    /// <inheritdoc />
    public void Dispose()
    {
        _http.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>尝试刷新令牌。交给 <see cref="UnauthorizedHandler" /> 在 401 时调用。</summary>
    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        var refreshToken = Tokens.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        try
        {
            var result = await OAuth.RefreshAsync(refreshToken, cancellationToken).ConfigureAwait(false);

            Tokens.AccessToken = result.AccessToken;
            if (!string.IsNullOrWhiteSpace(result.RefreshToken))
            {
                Tokens.RefreshToken = result.RefreshToken;
            }

            Tokens.Origin = TokenOrigin.OAuth2;
            return true;
        }
        catch (Exception)
        {
            // 刷新失败一律按登录失效处理,由调用方引导重新登录。
            return false;
        }
    }

    private void RaiseUnauthorized() => UnauthorizedDetected?.Invoke();
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using LoliaFrpClient.Constants;
using LoliaFrpClient.Core;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace LoliaFrpClient.Services;

public class ApiClientProvider : IDisposable
{
    private static readonly Lazy<ApiClientProvider> _instance = new(() => new ApiClientProvider());
    private static readonly HttpRequestOptionsKey<bool> RetryAfterRefreshOptionKey = new("RetryAfterRefresh");
    private readonly SettingsStorage _settings = SettingsStorage.Instance;
    private readonly object _clientLock = new();
    private ApiClient? _apiClient;
    private HttpClient? _httpClient;

    private ApiClientProvider() { InitializeClient(); }
    public static ApiClientProvider Instance => _instance.Value;

    public ApiClient Client
    {
        get
        {
            lock (_clientLock)
            {
                return _apiClient ??= InitializeClient();
            }
        }
    }

    private ApiClient InitializeClient()
    {
        var baseUrl = AppConstants.ApiBaseUrl;

        IAuthenticationProvider authProvider = !string.IsNullOrEmpty(_settings.OAuthToken)
            ? new BearerTokenAuthenticationProvider(_settings)
            : new AnonymousAuthenticationProvider();

        // 1. 创建 Kiota 默认的处理链
        var handlers = KiotaClientFactory.CreateDefaultHandlers();
        // 2. 将我们的 401 拦截器加入链条
        handlers.Add(new UnauthorizedInterceptorHandler());

        // 3. 使用带有拦截器的 HttpClient
        var httpClient = KiotaClientFactory.Create(handlers);
        var adapter = new HttpClientRequestAdapter(authProvider, httpClient: httpClient)
        {
            BaseUrl = baseUrl
        };

        // 记录 HttpClient 以便在重建时释放，避免连接池与 handler 泄漏
        _httpClient = httpClient;
        return _apiClient = new ApiClient(adapter);
    }

    /// <summary>
    ///     重建客户端。旧的 HttpClient 必须释放，否则每次登录/登出都会泄漏
    ///     一整套 handler 链与底层连接池。
    /// </summary>
    public void ReinitializeClient()
    {
        lock (_clientLock)
        {
            _apiClient = null;

            var previous = _httpClient;
            _httpClient = null;
            previous?.Dispose();

            InitializeClient();
        }
    }

    public void Dispose()
    {
        lock (_clientLock)
        {
            _apiClient = null;
            _httpClient?.Dispose();
            _httpClient = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 自定义拦截器：处理 401 状态码
    /// </summary>
    private class UnauthorizedInterceptorHandler : DelegatingHandler
    {
        private static readonly SemaphoreSlim RefreshLock = new(1, 1);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            if (request.Options.TryGetValue(RetryAfterRefreshOptionKey, out var hasRetried) && hasRetried)
            {
                _ = AuthSessionService.Instance.NotifyUnauthorizedAsync();
                return response;
            }

            var settings = SettingsStorage.Instance;
            var refreshToken = settings.RefreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _ = AuthSessionService.Instance.NotifyUnauthorizedAsync();
                return response;
            }

            var expiredAccessToken = settings.OAuthToken;
            var lockTaken = false;

            try
            {
                await RefreshLock.WaitAsync(cancellationToken);
                lockTaken = true;

                if (HasTokenChanged(request, expiredAccessToken, settings.OAuthToken))
                {
                    response.Dispose();
                    return await RetryWithCurrentTokenAsync(request, cancellationToken);
                }

                var tokenResponse = await OAuthTokenService.RefreshTokenAsync(refreshToken);
                settings.OAuthToken = tokenResponse.AccessToken;

                if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
                {
                    settings.RefreshToken = tokenResponse.RefreshToken;
                }

                response.Dispose();
                return await RetryWithCurrentTokenAsync(request, cancellationToken);
            }
            catch
            {
                _ = AuthSessionService.Instance.NotifyUnauthorizedAsync();
                return response;
            }
            finally
            {
                // 仅在实际取得锁时释放，避免异常路径下多释放导致信号量计数失衡。
                if (lockTaken) RefreshLock.Release();
            }
        }

        private static bool HasTokenChanged(HttpRequestMessage request, string? originalToken, string? currentToken)
        {
            if (string.IsNullOrWhiteSpace(currentToken) || string.Equals(originalToken, currentToken, StringComparison.Ordinal))
            {
                return false;
            }

            return TryGetBearerToken(request, out var requestToken) && string.Equals(requestToken, originalToken, StringComparison.Ordinal);
        }

        private async Task<HttpResponseMessage> RetryWithCurrentTokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var retryRequest = await CloneHttpRequestMessageAsync(request, cancellationToken);
            retryRequest.Options.Set(RetryAfterRefreshOptionKey, true);

            var currentToken = SettingsStorage.Instance.OAuthToken;
            if (!string.IsNullOrWhiteSpace(currentToken))
            {
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentToken);
            }

            return await base.SendAsync(retryRequest, cancellationToken);
        }

        private static bool TryGetBearerToken(HttpRequestMessage request, out string? token)
        {
            token = request.Headers.Authorization?.Scheme == "Bearer"
                ? request.Headers.Authorization.Parameter
                : null;
            return !string.IsNullOrWhiteSpace(token);
        }

        private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version,
                VersionPolicy = request.VersionPolicy
            };

            foreach (var option in request.Options)
            {
                clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
            }

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Content != null)
            {
                var memoryStream = new MemoryStream();
                await request.Content.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;

                var streamContent = new StreamContent(memoryStream);
                foreach (var header in request.Content.Headers)
                {
                    streamContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                clone.Content = streamContent;
            }

            return clone;
        }
    }

    private class BearerTokenAuthenticationProvider : IAuthenticationProvider
    {
        private readonly SettingsStorage _settings;
        public BearerTokenAuthenticationProvider(SettingsStorage settings) => _settings = settings;

        public Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalContext = null, CancellationToken cancellationToken = default)
        {
            var token = _settings.OAuthToken;
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
            }

            return Task.CompletedTask;
        }
    }
}

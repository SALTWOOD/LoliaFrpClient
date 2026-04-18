using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using LoliaFrpClient.Core;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace LoliaFrpClient.Services;

public class ApiClientProvider
{
    private static readonly Lazy<ApiClientProvider> _instance = new(() => new ApiClientProvider());
    private static readonly HttpRequestOptionsKey<bool> RetryAfterRefreshOptionKey = new("RetryAfterRefresh");
    private readonly SettingsStorage _settings = SettingsStorage.Instance;
    private ApiClient? _apiClient;

    private ApiClientProvider() { InitializeClient(); }
    public static ApiClientProvider Instance => _instance.Value;

    public ApiClient Client => _apiClient ??= InitializeClient();

    private ApiClient InitializeClient()
    {
        var baseUrl = "https://api.lolia.link/api/v1";

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

        return _apiClient = new ApiClient(adapter);
    }

    public void ReinitializeClient() => InitializeClient();

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

            try
            {
                await RefreshLock.WaitAsync(cancellationToken);

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
                if (RefreshLock.CurrentCount == 0)
                {
                    RefreshLock.Release();
                }
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

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
    private readonly SettingsStorage _settings = SettingsStorage.Instance;
    private ApiClient? _apiClient;

    private ApiClientProvider() { InitializeClient(); }
    public static ApiClientProvider Instance => _instance.Value;

    public ApiClient Client => _apiClient ??= InitializeClient();

    private ApiClient InitializeClient()
    {
        var baseUrl = "https://api.lolia.link/api/v1";
        var token = _settings.OAuthToken;

        IAuthenticationProvider authProvider = !string.IsNullOrEmpty(token) 
            ? new BearerTokenAuthenticationProvider(token) 
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
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _ = AuthSessionService.Instance.NotifyUnauthorizedAsync();
            }

            return response;
        }
    }

    private class BearerTokenAuthenticationProvider : IAuthenticationProvider
    {
        private readonly string _token;
        public BearerTokenAuthenticationProvider(string token) => _token = token;

        public Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalContext = null, CancellationToken cancellationToken = default)
        {
            request.Headers.Add("Authorization", $"Bearer {_token}");
            return Task.CompletedTask;
        }
    }
}

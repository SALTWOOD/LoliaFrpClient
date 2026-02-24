using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LoliaFrpClient.Core;
using LoliaFrpClient.Pages; // 假设是 WPF，如果是 WinForms 请改为 System.Windows.Forms
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LoliaFrpClient.Services;

public class ApiClientProvider
{
    private static readonly Lazy<ApiClientProvider> _instance = new(() => new ApiClientProvider());
    private readonly SettingsStorage _settings = SettingsStorage.Instance;
    private ApiClient? _apiClient;
    
    // 用于防止弹窗多次弹出的锁
    private static int _isShowingDialog = 0;

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
        // 获取 UI 线程的调度器
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = 
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // 原子锁防止多线程并发导致的弹窗堆叠
                if (Interlocked.CompareExchange(ref _isShowingDialog, 1, 0) == 0)
                {
                    // 切换到 UI 线程执行弹窗逻辑
                    _dispatcherQueue.TryEnqueue(async () =>
                    {
                        await ShowUnauthorizedDialog();
                    });
                }
            }

            return response;
        }

        private async Task ShowUnauthorizedDialog()
        {
            // 检查主窗口是否存在且已加载
            if (App.MainWindow?.Content?.XamlRoot is null)
            {
                Interlocked.Exchange(ref _isShowingDialog, 0);
                return;
            }

            var result = await DialogManager.Instance.ShowConfirmAsync(
                "登录已失效",
                "您的登录信息已过期，是否重新登录？",
                "是",
                "否");

            if (result == ContentDialogResult.Primary)
            {
                MainWindow.NavigateTo<Settings>();
            }
            else
            {
                Interlocked.Exchange(ref _isShowingDialog, 0);
            }
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
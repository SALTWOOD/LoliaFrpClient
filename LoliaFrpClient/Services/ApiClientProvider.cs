using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using LoliaFrpClient.Core;

namespace LoliaFrpClient.Services
{
    /// <summary>
    /// ApiClient 提供者，用于创建和管理 ApiClient 实例
    /// </summary>
    public class ApiClientProvider
    {
        private static readonly Lazy<ApiClientProvider> _instance = new Lazy<ApiClientProvider>(() => new ApiClientProvider());
        public static ApiClientProvider Instance => _instance.Value;

        private readonly SettingsStorage _settings = SettingsStorage.Instance;
        private ApiClient? _apiClient;

        private ApiClientProvider()
        {
            InitializeClient();
        }

        /// <summary>
        /// 获取 ApiClient 实例
        /// </summary>
        public ApiClient Client
        {
            get
            {
                if (_apiClient == null)
                {
                    InitializeClient();
                }
                return _apiClient!;
            }
        }

        /// <summary>
        /// 初始化客户端
        /// </summary>
        private void InitializeClient()
        {
            var baseUrl = "https://api.lolia.link/api/v1";
            var token = _settings.Authorization;
            
            // 使用自定义的认证提供者
            IAuthenticationProvider authProvider;
            if (!string.IsNullOrEmpty(token))
            {
                authProvider = new BearerTokenAuthenticationProvider(token);
            }
            else
            {
                authProvider = new AnonymousAuthenticationProvider();
            }
            
            var adapter = new HttpClientRequestAdapter(authProvider);
            
            // 设置 base URL
            adapter.BaseUrl = baseUrl;
            
            _apiClient = new ApiClient(adapter);
        }

        /// <summary>
        /// 重新初始化客户端（当设置更改时调用）
        /// </summary>
        public void ReinitializeClient()
        {
            InitializeClient();
        }

        /// <summary>
        /// Bearer Token 认证提供者
        /// </summary>
        private class BearerTokenAuthenticationProvider : IAuthenticationProvider
        {
            private readonly string _token;

            public BearerTokenAuthenticationProvider(string token)
            {
                _token = token;
            }

            public Task AuthenticateRequestAsync(Microsoft.Kiota.Abstractions.RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
            {
                request.Headers.Add("Authorization", $"Bearer {_token}");
                return Task.CompletedTask;
            }
        }
    }
}

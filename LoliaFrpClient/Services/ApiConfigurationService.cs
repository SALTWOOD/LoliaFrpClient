using System;
using LoliaFrpClient.Core.Client;
using LoliaFrpClient.Models;

namespace LoliaFrpClient.Services
{
    /// <summary>
    /// API Configuration service for managing API client settings
    /// </summary>
    public class ApiConfigurationService
    {
        private static readonly Lazy<ApiConfigurationService> _instance = new Lazy<ApiConfigurationService>(() => new ApiConfigurationService());
        public static ApiConfigurationService Instance => _instance.Value;

        private readonly SettingsStorage _settings = SettingsStorage.Instance;

        private ApiConfigurationService()
        {
        }

        /// <summary>
        /// Update API configuration with authorization token from settings
        /// </summary>
        public void UpdateConfiguration()
        {
            var config = GlobalConfiguration.Instance as Configuration;
            if (config != null)
            {
                string? authorization = _settings.Authorization;
                if (!string.IsNullOrEmpty(authorization))
                {
                    config.ApiKey["Authorization"] = authorization;
                    config.ApiKeyPrefix["Authorization"] = "Bearer";
                }
                else
                {
                    config.ApiKey.Remove("Authorization");
                    config.ApiKeyPrefix.Remove("Authorization");
                }
            }
        }

        /// <summary>
        /// Set authorization token
        /// </summary>
        public void SetAuthorization(string? authorization)
        {
            _settings.Authorization = authorization;
            UpdateConfiguration();
        }

        /// <summary>
        /// Get authorization token
        /// </summary>
        public string? GetAuthorization()
        {
            return _settings.Authorization;
        }

        /// <summary>
        /// Clear authorization token
        /// </summary>
        public void ClearAuthorization()
        {
            _settings.Authorization = null;
            UpdateConfiguration();
        }

        /// <summary>
        /// Handle login result and store token
        /// </summary>
        /// <param name="loginResult">Login result containing token and user info</param>
        public void HandleLoginResult(LoginResult loginResult)
        {
            if (loginResult != null && !string.IsNullOrEmpty(loginResult.Token))
            {
                SetAuthorization(loginResult.Token);
            }
            else
            {
                ClearAuthorization();
            }
        }
    }
}

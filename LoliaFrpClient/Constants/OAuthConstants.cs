namespace LoliaFrpClient.Constants
{
    /// <summary>
    /// OAuth 相关常量
    /// </summary>
    public static class OAuthConstants
    {
        /// <summary>
        /// OAuth 授权端点 URL
        /// </summary>
        public const string AuthorizeEndpoint = "https://dash.lolia.link/oauth/authorize";

        /// <summary>
        /// OAuth Token 端点 URL
        /// </summary>
        public const string TokenEndpoint = "https://api.lolia.link/api/v1/oauth2/token";

        /// <summary>
        /// OAuth 客户端 ID
        /// </summary>
        public const string ClientId = "6e3hz082f8f2t5s9";

        /// <summary>
        /// OAuth 客户端密钥
        /// </summary>
        public const string ClientSecret = "oioqkqmzbcb7s6xvauxobtv4fq2tw85j";

        /// <summary>
        /// OAuth 响应类型
        /// </summary>
        public const string ResponseType = "code";

        /// <summary>
        /// OAuth 授权范围
        /// </summary>
        public const string Scope = "all";

        /// <summary>
        /// OAuth 回调端口
        /// </summary>
        public const int CallbackPort = 56721;

        /// <summary>
        /// OAuth 回调路径
        /// </summary>
        public const string CallbackPath = "/callback";
    }
}

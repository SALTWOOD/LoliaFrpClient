namespace LoliaFrpClient.Models
{
    /// <summary>
    /// Login result containing token and user information
    /// </summary>
    public class LoginResult
    {
        /// <summary>
        /// Authorization token
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// User information (currently not used)
        /// </summary>
        public object? UserInfo { get; set; }
    }
}

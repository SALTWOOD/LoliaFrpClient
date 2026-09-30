namespace LoliaFrpClient.Core;

public sealed class OAuthOptions
{
    public string AuthorizeEndpoint { get; init; } = "https://dash.lolia.link/oauth/authorize";

    public string TokenEndpoint { get; init; } = "https://api.lolia.link/api/v1/oauth2/token";

    public string ClientId { get; init; } = "goibzooz0s14ntgc";

    public string Scope { get; init; } = "all";

    public int CallbackPort { get; init; } = 56721;

    public string CallbackPath { get; init; } = "/callback";

    public string CallbackUri => $"http://localhost:{CallbackPort}{CallbackPath}";
}
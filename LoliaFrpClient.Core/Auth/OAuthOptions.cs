namespace LoliaFrpClient.Core;

public sealed class OAuthOptions
{
    public string DeviceAuthorizationEndpoint { get; init; } = "https://api.lolia.link/api/v1/oauth2/device/code";

    public string TokenEndpoint { get; init; } = "https://api.lolia.link/api/v1/oauth2/token";

    public string ClientId { get; init; } = "goibzooz0s14ntgc";

    public string Scope { get; init; } = "all";
}

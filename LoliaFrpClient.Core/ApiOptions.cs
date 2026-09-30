namespace LoliaFrpClient.Core;

public sealed class ApiOptions
{
    public string BaseUrl { get; init; } = "https://api.lolia.link/api/v1";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public OAuthOptions OAuth { get; init; } = new();
}
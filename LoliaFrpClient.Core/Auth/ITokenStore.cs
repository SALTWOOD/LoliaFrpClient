namespace LoliaFrpClient.Core;

public interface ITokenStore
{
    string? AccessToken { get; set; }

    string? RefreshToken { get; set; }

    TokenOrigin Origin { get; set; }

    void Clear();
}
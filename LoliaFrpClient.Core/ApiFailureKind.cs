namespace LoliaFrpClient.Core;

public enum ApiFailureKind
{
    None = 0,
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    Server,
    Network,
    Unknown
}
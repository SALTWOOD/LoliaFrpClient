namespace LoliaFrpClient.Core.Frpc;

public sealed record FrpcLaunchCredential
{
    // Numeric server-side id, used as the "id" half of frpc's -t id:token.
    public required int TunnelId { get; init; }

    // Equivalent to a password. Never log or display it.
    public required string Token { get; init; }
}
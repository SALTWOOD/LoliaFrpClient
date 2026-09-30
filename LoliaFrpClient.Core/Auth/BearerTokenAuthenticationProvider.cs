using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace LoliaFrpClient.Core;

public sealed class BearerTokenAuthenticationProvider : IAuthenticationProvider
{
    private readonly ITokenStore _tokens;

    public BearerTokenAuthenticationProvider(ITokenStore tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var token = _tokens.AccessToken;
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Add("Authorization", $"Bearer {token}");

        return Task.CompletedTask;
    }
}
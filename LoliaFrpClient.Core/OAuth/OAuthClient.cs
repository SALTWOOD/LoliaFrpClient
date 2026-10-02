using System.Text.Json;

namespace LoliaFrpClient.Core;

public sealed class OAuthClient
{
    private const string DeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";

    private readonly HttpClient _http;
    private readonly OAuthOptions _options;

    public OAuthClient(OAuthOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<OAuthDeviceCode> RequestDeviceCodeAsync(CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["scope"] = _options.Scope
        });

        using var response = await _http
            .PostAsync(_options.DeviceAuthorizationEndpoint, content, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"设备授权请求失败:{(int)response.StatusCode} - {body}");

        return JsonSerializer.Deserialize(body, OAuthJsonContext.Default.OAuthDeviceCode)
               ?? throw new JsonException("设备授权响应无法解析。");
    }

    public async Task<OAuthTokenResponse> PollForTokenAsync(OAuthDeviceCode device,
        CancellationToken cancellationToken = default)
    {
        var interval = TimeSpan.FromSeconds(device.Interval > 0 ? device.Interval : 5);

        using var expiry = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        expiry.CancelAfter(TimeSpan.FromSeconds(device.ExpiresIn > 0 ? device.ExpiresIn : 900));

        while (true)
        {
            OAuthTokenResult result;
            try
            {
                await Task.Delay(interval, expiry.Token).ConfigureAwait(false);

                result = await SendTokenRequestAsync(
                    new Dictionary<string, string>
                    {
                        ["grant_type"] = DeviceCodeGrantType,
                        ["device_code"] = device.DeviceCode,
                        ["client_id"] = _options.ClientId
                    },
                    expiry.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (expiry.IsCancellationRequested &&
                                                     !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("设备码已过期,请重新登录。");
            }

            if (result.Token is not null) return result.Token;

            switch (result.Error)
            {
                case "authorization_pending":
                    break;

                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    break;

                case "access_denied":
                    throw new InvalidOperationException("用户拒绝了授权。");

                case "expired_token":
                    throw new TimeoutException("设备码已过期,请重新登录。");

                default:
                    throw new HttpRequestException($"令牌请求失败:{result.Error} - {result.ErrorDescription}");
            }
        }
    }

    public async Task<OAuthTokenResponse> RefreshAsync(string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var result = await SendTokenRequestAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = _options.ClientId
            },
            cancellationToken).ConfigureAwait(false);

        return result.Token ??
               throw new HttpRequestException($"刷新令牌失败:{result.Error} - {result.ErrorDescription}");
    }

    private async Task<OAuthTokenResult> SendTokenRequestAsync(
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(parameters);
        using var response =
            await _http.PostAsync(_options.TokenEndpoint, content, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize(body, OAuthJsonContext.Default.OAuthErrorResponse);
            if (error?.Error is { Length: > 0 } code)
                return new OAuthTokenResult(null, code, error.ErrorDescription);

            throw new HttpRequestException($"令牌请求失败:{(int)response.StatusCode} - {body}");
        }

        return new OAuthTokenResult(
            JsonSerializer.Deserialize(body, OAuthJsonContext.Default.OAuthTokenResponse)
            ?? throw new JsonException("令牌响应无法解析。"),
            null,
            null);
    }

    private sealed record OAuthTokenResult(OAuthTokenResponse? Token, string? Error, string? ErrorDescription);
}

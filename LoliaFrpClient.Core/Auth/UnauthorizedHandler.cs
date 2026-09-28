using System.Net;
using System.Net.Http.Headers;

namespace LoliaFrpClient.Core;

/// <summary>
///     401 拦截器:收到 401 时尝试刷新令牌并重放一次请求。
/// </summary>
/// <remarks>
///     <para>
///         刷新用信号量串行化。并发的多个请求同时撞上 401 时,只有第一个真正去刷新,
///         其余的在拿到锁后会发现令牌已被换掉,直接用新令牌重放,避免把刷新端点打爆。
///     </para>
///     <para>
///         重放前会克隆原始请求(含请求体),否则 POST 的 body 已被消费无法重发。
///     </para>
///     <para>
///         重放只做一次:请求上打的 <see cref="RetriedKey" /> 标记确保不会陷入刷新循环。
///     </para>
/// </remarks>
internal sealed class UnauthorizedHandler : DelegatingHandler
{
    private static readonly HttpRequestOptionsKey<bool> RetriedKey = new("LoliaFrp_RetriedAfterRefresh");

    private readonly Func<CancellationToken, Task<bool>> _refresh;
    private readonly Action _onUnauthorized;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly ITokenStore _tokens;

    /// <summary>创建拦截器。</summary>
    /// <param name="tokens">凭证存储。</param>
    /// <param name="refresh">刷新委托。返回 <c>true</c> 表示令牌已更新,可以重放请求。</param>
    /// <param name="onUnauthorized">刷新失败或无法刷新时的回调,用于通知宿主重新登录。</param>
    public UnauthorizedHandler(ITokenStore tokens, Func<CancellationToken, Task<bool>> refresh, Action onUnauthorized)
    {
        _tokens = tokens;
        _refresh = refresh;
        _onUnauthorized = onUnauthorized;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // 已经重放过一次,不再重试,直接判定登录失效。
        if (request.Options.TryGetValue(RetriedKey, out var alreadyRetried) && alreadyRetried)
        {
            _onUnauthorized();
            return response;
        }

        if (string.IsNullOrWhiteSpace(_tokens.RefreshToken))
        {
            _onUnauthorized();
            return response;
        }

        var expiredToken = _tokens.AccessToken;
        var lockTaken = false;

        try
        {
            await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            lockTaken = true;

            // 等锁期间别的请求可能已经刷过了,直接用新令牌重放。
            if (HasTokenChanged(request, expiredToken, _tokens.AccessToken))
            {
                response.Dispose();
                return await RetryAsync(request, cancellationToken).ConfigureAwait(false);
            }

            if (!await _refresh(cancellationToken).ConfigureAwait(false))
            {
                _onUnauthorized();
                return response;
            }

            response.Dispose();
            return await RetryAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _onUnauthorized();
            return response;
        }
        finally
        {
            if (lockTaken)
            {
                _refreshGate.Release();
            }
        }
    }

    /// <summary>判断令牌是否已被别的请求换掉——只在原请求确实带的是那个过期令牌时才成立。</summary>
    private static bool HasTokenChanged(HttpRequestMessage request, string? originalToken, string? currentToken)
    {
        if (string.IsNullOrWhiteSpace(currentToken) ||
            string.Equals(originalToken, currentToken, StringComparison.Ordinal))
        {
            return false;
        }

        return TryGetBearerToken(request, out var requestToken) &&
               string.Equals(requestToken, originalToken, StringComparison.Ordinal);
    }

    private static bool TryGetBearerToken(HttpRequestMessage request, out string? token)
    {
        token = request.Headers.Authorization?.Scheme == "Bearer"
            ? request.Headers.Authorization.Parameter
            : null;

        return !string.IsNullOrWhiteSpace(token);
    }

    private async Task<HttpResponseMessage> RetryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var retryRequest = await CloneAsync(request, cancellationToken).ConfigureAwait(false);
        retryRequest.Options.Set(RetriedKey, true);

        var currentToken = _tokens.AccessToken;
        if (!string.IsNullOrWhiteSpace(currentToken))
        {
            retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentToken);
        }

        return await base.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;

            var content = new StreamContent(buffer);
            foreach (var header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        return clone;
    }
}

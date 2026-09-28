namespace LoliaFrpClient.Core;

/// <summary>
///     把一次完整的 OAuth2 授权码流程串起来:起本地回调监听 → 打开浏览器 → 收授权码 →
///     换令牌 → 写入会话 → 取回用户资料。
/// </summary>
/// <remarks>
///     打开浏览器这一步由宿主提供(<paramref name="openBrowser" /> 的参数),Core 是类库,
///     不能依赖任何 UI 框架的启动器。桌面宿主传 Avalonia 的 <c>ILauncher</c>,测试传一个记录
///     参数的委托即可。
/// </remarks>
public static class OAuthLogin
{
    /// <summary>等待用户在浏览器里完成授权的默认上限。</summary>
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromMinutes(5);

    /// <summary>走一遍授权码 + PKCE 流程并登录。</summary>
    /// <param name="openBrowser">接收授权页地址并把它打开的委托。</param>
    /// <param name="session">目标会话。为 <c>null</c> 时使用 <see cref="ApiSession.Current" />。</param>
    /// <param name="timeout">等待回调的上限。为 <c>null</c> 时用 <see cref="DefaultTimeout" />。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>
    ///     成功时 <c>Data</c> 是取回资料的 <see cref="User" />;令牌已写入 <see cref="ApiSession.Tokens" />。
    ///     失败时 <c>Failure</c> 区分是端口占用(<c>Network</c>)、用户拒绝(<c>Business</c>)
    ///     还是令牌端点出错(<c>Server</c>)。
    /// </returns>
    public static async Task<ApiResult<User>> SignInAsync(
        Func<string, Task> openBrowser,
        ApiSession? session = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(openBrowser);
        var api = session ?? ApiSession.Current;

        OAuthLoopbackListener listener;
        string authorizeUrl;
        try
        {
            // 先开始监听再打开浏览器:反向顺序会漏掉浏览器极快返回的回调。
            authorizeUrl = api.OAuth.BeginAuthorization();
            listener = new OAuthLoopbackListener(api.Options.OAuth);
        }
        catch (Exception ex)
        {
            return Failure($"无法启动本地回调监听:{ex.Message}", ApiFailureKind.Network);
        }

        await using (listener)
        {
            try
            {
                await openBrowser(authorizeUrl).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return Failure($"无法打开浏览器:{ex.Message}", ApiFailureKind.Network);
            }

            OAuthCallback callback;
            try
            {
                callback = await listener.WaitForCallbackAsync(timeout ?? DefaultTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                return Failure(ex.Message, ApiFailureKind.Network);
            }

            if (!string.IsNullOrEmpty(callback.Error))
            {
                var detail = string.IsNullOrEmpty(callback.ErrorDescription)
                    ? callback.Error
                    : $"{callback.Error}:{callback.ErrorDescription}";
                return Failure($"授权未通过({detail})。", ApiFailureKind.Business);
            }

            if (string.IsNullOrEmpty(callback.Code))
            {
                return Failure("回调地址上没有授权码。", ApiFailureKind.Business);
            }

            OAuthTokenResponse token;
            try
            {
                token = await api.OAuth.ExchangeCodeAsync(callback.Code, callback.State, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                // state 校验失败或没先发起授权。重试即可,不是服务端故障。
                return Failure(ex.Message, ApiFailureKind.Business);
            }
            catch (Exception ex)
            {
                return Failure($"换取令牌失败:{ex.Message}", ApiFailureKind.Server);
            }

            api.Tokens.AccessToken = token.AccessToken;
            api.Tokens.RefreshToken = token.RefreshToken;
            api.Tokens.Origin = TokenOrigin.OAuth2;

            var me = await User.MeAsync(api, cancellationToken).ConfigureAwait(false);
            if (me.IsSuccess)
            {
                return me;
            }

            // 令牌已经到手,登录本身是成功的;资料没取到不该让调用方以为没登上。
            return new ApiResult<User>
            {
                IsSuccess = true,
                Code = 200,
                Msg = me.Msg,
                Data = new User(api)
            };
        }
    }

    private static ApiResult<User> Failure(string msg, ApiFailureKind kind) => new()
    {
        IsSuccess = false,
        Code = 0,
        Msg = msg,
        Failure = kind
    };
}

namespace LoliaFrpClient.Core;

public static class OAuthLogin
{
    public static async Task<ApiResult<User>> SignInAsync(
        IProgress<OAuthDeviceCode>? prompt = null,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        OAuthDeviceCode device;
        try
        {
            device = await api.OAuth.RequestDeviceCodeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Failure($"无法申请设备码:{ex.Message}", ApiFailureKind.Network);
        }

        prompt?.Report(device);

        OAuthTokenResponse token;
        try
        {
            token = await api.OAuth.PollForTokenAsync(device, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Failure(ex.Message, ApiFailureKind.BadRequest);
        }
        catch (TimeoutException ex)
        {
            return Failure(ex.Message, ApiFailureKind.Network);
        }
        catch (Exception ex)
        {
            return Failure($"换取令牌失败:{ex.Message}", ApiFailureKind.Server);
        }

        api.Tokens.AccessToken = token.AccessToken;
        api.Tokens.RefreshToken = token.RefreshToken;
        api.Tokens.Origin = TokenOrigin.OAuth2;

        var me = await User.MeAsync(api, cancellationToken).ConfigureAwait(false);
        if (me.IsSuccess) return me;

        return new ApiResult<User>
        {
            IsSuccess = true,
            Code = 200,
            Msg = me.Msg,
            Data = new User(api)
        };
    }

    private static ApiResult<User> Failure(string msg, ApiFailureKind kind)
    {
        return new ApiResult<User>
        {
            IsSuccess = false,
            Code = 0,
            Msg = msg,
            Failure = kind
        };
    }
}

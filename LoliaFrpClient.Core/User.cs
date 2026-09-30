using LoliaFrpClient.Api.User.Checkin;
using LoliaFrpClient.Api.User.Frpc.Config;
using LoliaFrpClient.Api.User.Info;
using LoliaFrpClient.Api.User.Login;
using LoliaFrpClient.Api.User.Logout;
using LoliaFrpClient.Api.User.Nodes;
using LoliaFrpClient.Api.User.Register;
using LoliaFrpClient.Api.User.ResetPassword;
using LoliaFrpClient.Api.User.SendEmail;
using LoliaFrpClient.Api.User.Sessions;
using LoliaFrpClient.Api.User.Tunnel;
using LoliaFrpClient.Api.User.Username;
using SponsorNodes = LoliaFrpClient.Api.User.Sponsor.Nodes;

// Our Tunnel class shadows the same-named namespace, so alias the Api side.
using ApiTunnel = LoliaFrpClient.Api.User.Tunnel;

namespace LoliaFrpClient.Core;

// Aggregate root: authentication enters here, tunnels come out.
// The session flows through constructors, so callers never pass credentials around.
public sealed class User : ApiFacade
{
    public User()
    {
    }

    public User(ApiSession session) : base(session)
    {
    }

    // Only LoginAsync fills this.
    public int? Id { get; private init; }

    public string? Username { get; private init; }

    public string? Email { get; private init; }

    public string? Avatar { get; private init; }

    // KycStatus, Role and TodayChecked come only from MeAsync.
    public string? KycStatus { get; private init; }

    public string? Role { get; private init; }

    // Only MeAsync fills this. True means a check-in now returns 400.
    public bool? TodayChecked { get; private init; }

    public bool IsAuthenticated => Session.IsAuthenticated;

    #region Static entry points

    // Password login; stores the issued JWT on the session.
    // captchaToken is required only when the server demands it.
    public static async Task<ApiResult<User>> LoginAsync(
        string username,
        string password,
        bool keepLogin = true,
        string? captchaToken = null,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        var result = await ApiCall.RunAsync<LoginPostResponse, LoginPostResponse_data>(
            c => api.Client.User.Login.PostAsLoginPostResponseAsync(
                new LoginPostRequestBody
                {
                    Username = username,
                    Password = password,
                    KeepLogin = keepLogin,
                    CaptchaToken = captchaToken
                },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess || result.Data is not { } data) return result.With<User>(null);

        api.Tokens.AccessToken = data.Token;
        api.Tokens.RefreshToken = null;
        api.Tokens.Origin = TokenOrigin.UserJwt;

        return result.With(new User(api)
        {
            Id = data.Id,
            Username = data.Username,
            Email = data.Email,
            Avatar = data.Avatar
        });
    }

    // Registration does not sign in; call LoginAsync afterwards.
    public static Task<ApiResult> RegisterAsync(
        string username,
        string email,
        string password,
        string emailCode,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<RegisterPostResponse>(
            c => api.Client.User.Register.PostAsRegisterPostResponseAsync(
                new RegisterPostRequestBody
                {
                    Username = username,
                    Email = email,
                    Password = password,
                    Code = emailCode
                },
                cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }

    // Shared by registration and password reset.
    public static Task<ApiResult> SendEmailCodeAsync(
        string email,
        string? captchaToken = null,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<SendEmailPostResponse>(
            c => api.Client.User.SendEmail.PostAsSendEmailPostResponseAsync(
                new SendEmailPostRequestBody { Email = email, CaptchaToken = captchaToken },
                cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }

    public static Task<ApiResult> ResetPasswordAsync(
        string email,
        string emailCode,
        string newPassword,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<ResetPasswordPostResponse>(
            c => api.Client.User.ResetPassword.PostAsResetPasswordPostResponseAsync(
                new ResetPasswordPostRequestBody
                {
                    Email = email,
                    Code = emailCode,
                    Password = newPassword
                },
                cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }

    public static async Task<ApiResult<User>> MeAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        var result = await ApiCall.RunAsync<InfoGetResponse, InfoGetResponse_data>(
            c => api.Client.User.Info.GetAsInfoGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess || result.Data is not { } data) return result.With<User>(null);

        return result.With(new User(api)
        {
            Username = data.Username,
            Email = data.Email,
            Avatar = data.Avatar,
            KycStatus = data.KycStatus,
            Role = data.Role,
            TodayChecked = data.TodayChecked
        });
    }

    #endregion

    #region Instance operations

    // Daily check-in.
    // A repeat within 24h answers 400 with Failure = Business; that is not a fault.
    public Task<ApiResult<CheckinPostResponse_data>> CheckInAsync(CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<CheckinPostResponse, CheckinPostResponse_data>(
            c => Client.User.Checkin.PostAsCheckinPostResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken,
            // The cooldown 400 only declares code/msg, so recover typed data here.
            RecoverCheckinData);
    }

    public Task<ApiResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<LogoutPostResponse>(
            c => Client.User.Logout.PostAsLogoutPostResponseAsync(cancellationToken: c),
            // The 200 body is empty, so a returned response is the whole success signal.
            _ => ((int?)null, (string?)null),
            cancellationToken);
    }

    public Task<ApiResult> LogoutAllAsync(CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<Api.User.Logout.All.AllPostResponse>(
            c => Client.User.Logout.All.PostAsAllPostResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }

    public Task<ApiResult<UsernamePutResponse_data>> ChangeUsernameAsync(
        string newUsername,
        CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<UsernamePutResponse, UsernamePutResponse_data>(
            c => Client.User.Username.PutAsUsernamePutResponseAsync(
                new UsernamePutRequestBody { Username = newUsername },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public Task<ApiResult<SessionsGetResponse_data>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<SessionsGetResponse, SessionsGetResponse_data>(
            c => Client.User.Sessions.GetAsSessionsGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public Task<ApiResult> RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<Api.User.Sessions.Item.WithSession_DeleteResponse>(
            c => Client.User.Sessions[sessionId].DeleteAsWithSession_DeleteResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }

    public Task<ApiResult<NodesPostResponse_data>> GetNodesAsync(
        int? page = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<NodesPostResponse, NodesPostResponse_data>(
            c => Client.User.Nodes.PostAsNodesPostResponseAsync(
                new NodesPostRequestBody { Page = page, Limit = limit },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public Task<ApiResult<SponsorNodes.NodesGetResponse_data>> GetSponsorNodesAsync(
        CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<SponsorNodes.NodesGetResponse, SponsorNodes.NodesGetResponse_data>(
            c => Client.User.Sponsor.Nodes.GetAsNodesGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public Task<ApiResult<ConfigGetResponse_data>> GetFrpcConfigAsync(CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<ConfigGetResponse, ConfigGetResponse_data>(
            c => Client.User.Frpc.Config.GetAsConfigGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public Task<ApiResult<ApiTunnel.Token.Reset.ResetPostResponse_data>> ResetTunnelTokenAsync(
        int nodeId,
        CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<ApiTunnel.Token.Reset.ResetPostResponse, ApiTunnel.Token.Reset.ResetPostResponse_data>(
            c => Client.User.Tunnel.Token.Reset.PostAsResetPostResponseAsync(
                new ApiTunnel.Token.Reset.ResetPostRequestBody { NodeId = nodeId },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    #endregion

    #region Tunnels

    // The returned Tunnel is already bound to this user's session.
    public async Task<ApiResult<Tunnel>> CreateTunnelAsync(
        TunnelPostRequestBody specification,
        CancellationToken cancellationToken = default)
    {
        var result = await ApiCall.RunAsync<TunnelPostResponse, TunnelPostResponse_data>(
            c => Client.User.Tunnel.PostAsTunnelPostResponseAsync(specification, cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        var tunnel = result is { IsSuccess: true, Data.Name: { } name } ? new Tunnel(name, Session) : null;
        return result.With(tunnel);
    }

    // Delegates to Tunnel.ListAsync so both entry points return identical entities.
    public Task<ApiResult<IReadOnlyList<Tunnel>>> ListTunnelsAsync(CancellationToken cancellationToken = default)
    {
        return Tunnel.ListAsync(Session, cancellationToken);
    }

    public Task<ApiResult<Tunnel>> GetTunnelAsync(string name, CancellationToken cancellationToken = default)
    {
        return Tunnel.GetAsync(name, Session, cancellationToken);
    }

    // "Ref" must stay in the name: called Tunnel, it would shadow the type itself.
    public Tunnel TunnelRef(string name)
    {
        return new Tunnel(name, Session);
    }

    #endregion

    // Recovers the typed check-in payload from an untyped failure response.
    private static CheckinPostResponse_data? RecoverCheckinData(object raw)
    {
        if (raw is not System.Text.Json.JsonElement element ||
            element.ValueKind != System.Text.Json.JsonValueKind.Object)
            return null;

        try
        {
            return new Microsoft.Kiota.Serialization.Json.JsonParseNode(element)
                .GetObjectValue(CheckinPostResponse_data.CreateFromDiscriminatorValue);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
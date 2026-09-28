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

// 本项目的 Tunnel 类会遮挡同名命名空间,故对 Api 侧的 Tunnel 命名空间取别名。
using ApiTunnel = LoliaFrpClient.Api.User.Tunnel;

namespace LoliaFrpClient.Core;

/// <summary>
///     用户聚合根。认证从这里进入,隧道等实体从这里产出。
///     <para>
///         会话贯通靠构造链:<see cref="LoginAsync" /> 产出绑定会话的 <see cref="User" />,
///         <see cref="CreateTunnelAsync" /> 产出的 <see cref="Tunnel" /> 继承同一个会话,
///         因此凭证不需要层层传递。
///     </para>
/// </summary>
public sealed class User : ApiFacade
{
    /// <summary>用当前会话构造。适用于已登录、只想调接口的场景。</summary>
    public User()
    {
    }

    /// <summary>用指定会话构造。</summary>
    public User(ApiSession session) : base(session)
    {
    }

    /// <summary>用户 ID。仅在 <see cref="LoginAsync" /> 的返回中有值。</summary>
    public int? Id { get; private init; }

    /// <summary>用户名。登录后由服务端返回,或经 <see cref="MeAsync" /> 取得。</summary>
    public string? Username { get; private init; }

    /// <summary>邮箱。</summary>
    public string? Email { get; private init; }

    /// <summary>头像地址。</summary>
    public string? Avatar { get; private init; }

    /// <summary>实名认证状态。仅 <see cref="MeAsync" /> 会填充。</summary>
    public string? KycStatus { get; private init; }

    /// <summary>角色。仅 <see cref="MeAsync" /> 会填充。</summary>
    public string? Role { get; private init; }

    /// <summary>当前会话是否已持有凭证。</summary>
    public bool IsAuthenticated => Session.IsAuthenticated;

    #region 静态入口

    /// <summary>
    ///     密码登录。成功后把签发的用户 JWT 写入会话,后续调用自动携带。
    /// </summary>
    /// <param name="username">用户名。</param>
    /// <param name="password">密码。</param>
    /// <param name="keepLogin">是否延长会话有效期。</param>
    /// <param name="captchaToken">验证码令牌。服务端要求时必填。</param>
    /// <param name="session">目标会话。为 <c>null</c> 时使用 <see cref="ApiSession.Current" />。</param>
    /// <param name="cancellationToken">取消标记。</param>
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

        if (!result.IsSuccess || result.Data is not { } data)
        {
            return result.With<User>(null);
        }

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

    /// <summary>注册新账号。成功后需要再调用 <see cref="LoginAsync" />。</summary>
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

    /// <summary>发送邮箱验证码。注册与找回密码共用。</summary>
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

    /// <summary>用邮箱验证码重置密码。</summary>
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

    /// <summary>取当前登录用户的资料。</summary>
    public static async Task<ApiResult<User>> MeAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        var result = await ApiCall.RunAsync<InfoGetResponse, InfoGetResponse_data>(
            c => api.Client.User.Info.GetAsInfoGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess || result.Data is not { } data)
        {
            return result.With<User>(null);
        }

        return result.With(new User(api)
        {
            Username = data.Username,
            Email = data.Email,
            Avatar = data.Avatar,
            KycStatus = data.KycStatus,
            Role = data.Role
        });
    }

    #endregion

    #region 自身操作

    /// <summary>
    ///     每日签到。
    ///     <para>
    ///         24 小时内重复签到会失败,但不是故障:此时 <c>IsSuccess</c> 为 <c>false</c>、
    ///         <c>Failure</c> 为 <see cref="ApiFailureKind.Business" />、<c>Code</c> 为 400,
    ///         <c>Msg</c> 里带剩余时间,<c>Data</c> 里是上一次获得的流量。
    ///     </para>
    /// </summary>
    public Task<ApiResult<CheckinPostResponse_data>> CheckInAsync(CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<CheckinPostResponse, CheckinPostResponse_data>(
            c => Client.User.Checkin.PostAsCheckinPostResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken,
            // 冷却期的 400 只声明了 code/msg,data 落在 AdditionalData 里,这里补一次强类型恢复。
            RecoverCheckinData);

    /// <summary>登出当前会话。</summary>
    public Task<ApiResult> LogoutAsync(CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<LogoutPostResponse>(
            c => Client.User.Logout.PostAsLogoutPostResponseAsync(cancellationToken: c),
            // 该端点的 200 响应体是空的,没有 code/msg 可读;请求返回即视为成功。
            _ => ((int?)null, (string?)null),
            cancellationToken);

    /// <summary>登出全部会话。</summary>
    public Task<ApiResult> LogoutAllAsync(CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<Api.User.Logout.All.AllPostResponse>(
            c => Client.User.Logout.All.PostAsAllPostResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);

    /// <summary>修改用户名。</summary>
    public Task<ApiResult<UsernamePutResponse_data>> ChangeUsernameAsync(
        string newUsername,
        CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<UsernamePutResponse, UsernamePutResponse_data>(
            c => Client.User.Username.PutAsUsernamePutResponseAsync(
                new UsernamePutRequestBody { Username = newUsername },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);

    /// <summary>取活跃会话列表。</summary>
    public Task<ApiResult<SessionsGetResponse_data>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<SessionsGetResponse, SessionsGetResponse_data>(
            c => Client.User.Sessions.GetAsSessionsGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);

    /// <summary>登出指定会话。</summary>
    public Task<ApiResult> RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<Api.User.Sessions.Item.WithSession_DeleteResponse>(
            c => Client.User.Sessions[sessionId].DeleteAsWithSession_DeleteResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);

    /// <summary>取可用节点列表(分页)。</summary>
    public Task<ApiResult<NodesPostResponse_data>> GetNodesAsync(
        int? page = null,
        int? limit = null,
        CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<NodesPostResponse, NodesPostResponse_data>(
            c => Client.User.Nodes.PostAsNodesPostResponseAsync(
                new NodesPostRequestBody { Page = page, Limit = limit },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);

    /// <summary>取我赞助的节点列表。</summary>
    public Task<ApiResult<SponsorNodes.NodesGetResponse_data>> GetSponsorNodesAsync(CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<SponsorNodes.NodesGetResponse, SponsorNodes.NodesGetResponse_data>(
            c => Client.User.Sponsor.Nodes.GetAsNodesGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);

    /// <summary>取 frpc 配置。</summary>
    public Task<ApiResult<ConfigGetResponse_data>> GetFrpcConfigAsync(CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<ConfigGetResponse, ConfigGetResponse_data>(
            c => Client.User.Frpc.Config.GetAsConfigGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);

    /// <summary>重置指定节点的隧道 Token。</summary>
    /// <param name="nodeId">节点 ID。</param>
    /// <param name="cancellationToken">取消标记。</param>
    public Task<ApiResult<ApiTunnel.Token.Reset.ResetPostResponse_data>> ResetTunnelTokenAsync(
        int nodeId,
        CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<ApiTunnel.Token.Reset.ResetPostResponse, ApiTunnel.Token.Reset.ResetPostResponse_data>(
            c => Client.User.Tunnel.Token.Reset.PostAsResetPostResponseAsync(
                new ApiTunnel.Token.Reset.ResetPostRequestBody { NodeId = nodeId },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);

    #endregion

    #region 隧道

    /// <summary>创建隧道。返回的 <see cref="Tunnel" /> 已绑定本用户所在的会话。</summary>
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

    /// <summary>取隧道列表。</summary>
    public async Task<ApiResult<IReadOnlyList<Tunnel>>> ListTunnelsAsync(CancellationToken cancellationToken = default)
    {
        var result = await ApiCall.RunAsync<TunnelGetResponse, TunnelGetResponse_data>(
            c => Client.User.Tunnel.GetAsTunnelGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Tunnel>? tunnels = result.IsSuccess
            ? [.. (result.Data?.List ?? []).Select(item => new Tunnel(item.Name, Session))]
            : null;

        return result.With(tunnels);
    }

    /// <summary>取单个隧道。</summary>
    public async Task<ApiResult<Tunnel>> GetTunnelAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await ApiCall.RunAsync<Api.User.Tunnel.Item.WithTunnel_nameGetResponse, Api.User.Tunnel.Item.WithTunnel_nameGetResponse_data>(
            c => Client.User.Tunnel[name].GetAsWithTunnel_nameGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken).ConfigureAwait(false);

        return result.With(result.IsSuccess ? new Tunnel(result.Data?.Name ?? name, Session) : null);
    }

    /// <summary>获取一个绑定在本会话上的隧道引用,不发起请求。</summary>
    public Tunnel Tunnel(string name) => new(name, Session);

    #endregion

    /// <summary>把失败响应里未经类型化的 <c>data</c> 还原成签到的强类型载荷。</summary>
    private static CheckinPostResponse_data? RecoverCheckinData(object raw)
    {
        if (raw is not System.Text.Json.JsonElement element ||
            element.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return null;
        }

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

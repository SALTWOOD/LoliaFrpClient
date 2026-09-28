# LoliaFrpClient.Core

对 `LoliaFrpClient.Api` 中 Kiota 生成代码的手写包装层。业务代码只应引用本项目。

## 为什么需要这一层

生成的客户端有三个问题让它无法直接用:

1. **非 2xx 一律抛异常。** 本 API 大量用 400 表达正常业务状态——签到冷却期内再次签到就是 400。
   若不拦截,调用方要为每个操作写 try/catch,而且无法区分「冷却中」和「网络断了」。
2. **无法直接 `new`。** 得手工装配 `IAuthenticationProvider` → `HttpClientRequestHandler`
   → `IRequestAdapter` → `ApiClient` 四层才能发一个请求。
3. **认证没接。** openapi.json 里 `securitySchemes` 是空的,生成的客户端不知道自己需要凭证。

这一层把三点都收进 `ApiCall.RunAsync` 一个方法里,门面方法都是薄壳。

## 用法

```csharp
// 应用启动时装配一次
ApiSession.Initialize(new ApiSession(
    options: new ApiOptions { BaseUrl = "https://api.lolia.link/api/v1" },
    tokens: new FileTokenStore()));

// 登录 —— 静态入口,产出绑定会话的 User
var login = await User.LoginAsync("my-name", "my-password");
if (!login.IsSuccess)
{
    Console.WriteLine($"[{login.Code}] {login.Msg}");
    return;
}

User me = login.Data!;
Console.WriteLine($"欢迎,{me.Username}");

// 用户自身操作
var checkin = await me.CheckInAsync();
if (checkin.IsSuccess)
    Console.WriteLine($"签到成功,+{checkin.Data!.TrafficGb} GB");
else if (checkin.IsBusinessFailure)
    Console.WriteLine(checkin.Msg);   // 「距离上次签到未满 24 小时,请于 3 小时 12 分钟后再来」

// 从 User 产出实体 —— 实体继承同一个会话,凭证自然贯通
var created = await me.CreateTunnelAsync(new TunnelPostRequestBody
{
    NodeId = 3, Type = "tcp", LocalIp = "127.0.0.1", LocalPort = 8080
});
Tunnel tunnel = created.Data!;

await tunnel.UpdateAsync(new WithTunnel_namePutRequestBody { Remark = "生产环境" });
await tunnel.DeleteAsync();

// 实体也提供静态入口
var tunnels = await Tunnel.ListAsync();
var one = await Tunnel.GetAsync("my-tunnel");
```

## 返回契约

所有方法返回 `ApiResult` / `ApiResult<T>`,不抛异常(调用方主动取消除外)。

```csharp
public bool   IsSuccess        { get; }   // HTTP 2xx 且业务码 200
public int    Code             { get; }   // 业务码,失败时为 HTTP 状态码
public string Msg              { get; }   // 服务端提示,可直接展示
public ApiFailureKind Failure  { get; }   // 见下
public object? ErrorData       { get; }   // 失败响应里未经类型化的 data
```

`Failure` 这一维是刻意保留的——光看 `Code` 数字会把三种需要不同处置的情况混在一起:

| Failure | 触发条件 | 调用方应做什么 |
|---|---|---|
| `None` | 成功 | — |
| `Business` | HTTP 400 | 展示 `Msg`,**不要重试**(如签到冷却) |
| `Unauthorized` | HTTP 401 | 触发重新登录 |
| `Forbidden` | HTTP 403 | 含「不支持 OAuth2 访问」与「账户已被封禁」两种语义,看 `Msg` |
| `NotFound` | HTTP 404 | 资源不存在 |
| `Server` | HTTP 5xx | 服务端故障,可稍后重试 |
| `Network` | 传输层异常 | 提示检查网络 |

## 门面类划分

**实体类**(有身份,提供静态查询 + 实例操作):`User`、`Tunnel`、`OAuthApp`、`Bill`、`Passkey`

**静态类**(无独立身份,只有静态方法):`Home`、`ClientVersion`、`Announcement`、`Traffic`、
`Domain`、`Kyc`、`QqBinding`、`Qqbot`、`OAuth2`

`Domain` 之所以是静态类而非实体:列表响应 `DomainGetResponse_data_domains` 没有暴露
`domain_id`,无法从返回值构造出带身份的实体,删除操作只能由调用方自行传入 ID。

## 认证

- 用户 JWT(来自 `/user/login`)与 OAuth2 Access Token(来自 `/oauth2/token`)是两套。
  `ApiSession.Tokens.Origin` 记录当前持有的是哪一种;文档明确部分接口对 OAuth2 令牌返回 403。
- 401 会自动尝试刷新并重放一次请求(`UnauthorizedHandler`)。刷新用信号量串行化,
  并发请求同时撞上 401 时只有一个真正去刷新。
- 刷新失败会触发 `ApiSession.UnauthorizedDetected`,宿主应用应在此跳转登录页。
- `OAuthClient` 实现了完整的 PKCE 授权码流程(无 client_secret)。

## 重新生成 SDK

生成的代码在 `LoliaFrpClient.Api`,**请勿手工修改**——重新生成会整个覆盖。

```bash
cd <repo-root>
kiota generate -l CSharp \
  -d LoliaFrpClient/openapi.json \
  -o LoliaFrpClient.Api \
  -n LoliaFrpClient.Api \
  -c ApiClient \
  --ad \
  --clean-output
```

参数含义与当初生成时 `kiota-lock.json` 的记录一致:客户端类名 `ApiClient`、命名空间
`LoliaFrpClient.Api`、类型可见性 `Public`、开启 `AdditionalData`、不开 backing store。
`serializer` / `deserializer` / `structured-mime-types` 用 kiota 默认值即可,与 lock 文件相符。

重新生成后需要人工复核的点:

- 是否有新增的 **400 业务态端点**——若有,考虑给它加 `recoverFromError`,否则失败响应里的
  `data` 只能通过 `ApiResult.ErrorData` 取到未类型化的原始值
- 响应信封是否仍为 `{code,msg,data}`。已知例外:`/user/logout`、`/oauth2/revoke` 无响应体;
  `/oauth2/token` 是 RFC 6749 扁平结构;`/user/traffic/tunnel/{id}` 用 `status` 而非 `code`
- 是否有 **路径签名冲突**。当前 `/user/traffic/tunnel/{tunnel_name}` 因与 `{tunnel_id}`
  归一化后相同而被 kiota 丢弃,该端点未包含在 SDK 中

## 已知限制

- **错误响应里的 `data` 没有强类型。** 生成的错误类型只声明 `code` 与 `msg`,
  其余字段落在 Kiota 的 `AdditionalData`。`ApiCall` 会把它原样放进 `ApiResult.ErrorData`;
  签到是唯一做了强类型恢复的端点(`User.RecoverCheckinData`),其正确性尚未对真实服务端验证。
- **`Node` 域未包装。** `/node/connect` 是 WebSocket 升级端点,没有普通请求方法,
  不能用 `ApiResult<T>` 表达,需要直接使用 `ClientWebSocket`。
- **`/user/traffic/tunnel/{tunnel_name}` 缺失**,原因见上。
- **Passkey 与 Kyc** 包装出的是请求骨架;端到端跑通还需要平台认证器(WebAuthn)
  与实名认证流程的配合,不在本层范围内。

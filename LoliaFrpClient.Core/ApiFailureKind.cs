namespace LoliaFrpClient.Core;

/// <summary>
///     失败的类别。
///     <para>
///         存在的理由:光看 <see cref="ApiResult.Code" /> 会把三种需要完全不同处置的情况混在一起——
///         「签到冷却中」不该重试、直接展示 msg;「token 失效」要触发重新登录;「网络断了」要提示重试。
///     </para>
/// </summary>
public enum ApiFailureKind
{
    /// <summary>未失败。</summary>
    None = 0,

    /// <summary>HTTP 400。业务态,例如签到冷却期内再次签到、参数校验不通过。</summary>
    Business,

    /// <summary>HTTP 401。凭证缺失、过期或已被吊销。</summary>
    Unauthorized,

    /// <summary>HTTP 403。包含「此接口不支持 OAuth2 访问」与「账户已被封禁」两种语义。</summary>
    Forbidden,

    /// <summary>HTTP 404。目标资源不存在。</summary>
    NotFound,

    /// <summary>HTTP 5xx。服务端故障,含 503「token 校验服务暂不可用」。</summary>
    Server,

    /// <summary>传输层异常:连不上、超时、TLS 失败。请求是否到达服务端未知。</summary>
    Network,

    /// <summary>未归类的异常。</summary>
    Unknown
}

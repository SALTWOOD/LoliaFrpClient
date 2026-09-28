namespace LoliaFrpClient.Core;

/// <summary>
///     不带数据的调用结果。用于 logout、delete 这类没有返回体的操作。
/// </summary>
public class ApiResult
{
    /// <summary>是否成功。<c>true</c> 表示 HTTP 2xx 且业务码为 200。</summary>
    public bool IsSuccess { get; init; }

    /// <summary>业务码。失败时为 HTTP 状态码,传输层失败时为 0。</summary>
    public int Code { get; init; }

    /// <summary>服务端返回的提示信息,可直接展示给用户。</summary>
    public string Msg { get; init; } = string.Empty;

    /// <summary>失败类别。成功时为 <see cref="ApiFailureKind.None" />。</summary>
    public ApiFailureKind Failure { get; init; } = ApiFailureKind.None;

    /// <summary>
    ///     失败响应体里未经类型化的 <c>data</c> 字段。
    ///     <para>
    ///         生成的错误类型只声明了 <c>code</c> 与 <c>msg</c>,文档里承诺的 <c>data</c>
    ///         会落进 Kiota 的 AdditionalData。这里原样取出来以免信息丢失;
    ///         需要强类型时由具体门面方法通过 <c>recoverFromError</c> 参数转换。
    ///     </para>
    /// </summary>
    public object? ErrorData { get; init; }

    /// <summary>是否因认证问题失败。调用方通常应据此触发重新登录。</summary>
    public bool IsUnauthorized => Failure == ApiFailureKind.Unauthorized;

    /// <summary>是否为业务态失败(参数不合法、冷却期内重复操作等)。这类失败重试无意义。</summary>
    public bool IsBusinessFailure => Failure == ApiFailureKind.Business;

    /// <summary>
    ///     换一个载荷类型,保留全部状态。
    ///     <para>门面方法用它把原始 DTO 换成绑定会话的实体对象,例如 <c>ApiResult&lt;Tunnel&gt;</c>。</para>
    /// </summary>
    public ApiResult<TOther> With<TOther>(TOther? data) => new()
    {
        IsSuccess = IsSuccess,
        Code = Code,
        Msg = Msg,
        Failure = Failure,
        ErrorData = ErrorData,
        Data = data
    };
}

/// <summary>
///     带数据的调用结果。
/// </summary>
/// <typeparam name="T">响应体 <c>data</c> 字段的类型,直接使用 <c>LoliaFrpClient.Api</c> 中生成的 DTO。</typeparam>
public sealed class ApiResult<T> : ApiResult
{
    /// <summary>响应体中的 <c>data</c>。成功但服务端未返回数据时为 <c>null</c>。</summary>
    public T? Data { get; init; }

    /// <summary>把载荷映射为另一种类型,保留全部状态。</summary>
    public ApiResult<TOther> Map<TOther>(Func<T, TOther?> selector) => new()
    {
        IsSuccess = IsSuccess,
        Code = Code,
        Msg = Msg,
        Failure = Failure,
        ErrorData = ErrorData,
        Data = IsSuccess && Data is not null ? selector(Data) : default
    };
}

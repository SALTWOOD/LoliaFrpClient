using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

namespace LoliaFrpClient.Core;

/// <summary>
///     把 Kiota 的调用结果(含抛出的异常)统一转换为 <see cref="ApiResult" />。
///     <para>
///         存在的理由:Kiota 对非 2xx 一律抛 <see cref="ApiException" />。而本 API 大量用 400
///         表达正常业务状态——例如签到冷却期内再次签到。若不在这里拦截,调用方就得为每个
///         操作写 try/catch,并且无法把「冷却中」和「网络断了」区分开。
///     </para>
///     <para>
///         这是整个包装层唯一有逻辑的地方,门面方法都只是薄壳。
///     </para>
/// </summary>
public static class ApiCall
{
    /// <summary>缓存各生成错误类型上的 <c>Msg</c> 属性。生成的错误类型没有公共接口,只能反射。</summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> MsgProperties = new();

    /// <summary>执行一次带数据返回的调用。</summary>
    /// <typeparam name="TResponse">Kiota 响应类型。</typeparam>
    /// <typeparam name="T">响应体中 <c>data</c> 的类型。</typeparam>
    /// <param name="call">实际发起请求的委托。</param>
    /// <param name="project">从响应对象中取出业务码、消息与数据。</param>
    /// <param name="cancellationToken">取消标记。调用方主动取消时会原样抛出,不转换为失败结果。</param>
    /// <param name="recoverFromError">
    ///     可选的失败数据恢复器。失败响应体里的 <c>data</c> 未经类型化,需要强类型时在此转换。
    /// </param>
    public static async Task<ApiResult<T>> RunAsync<TResponse, T>(
        Func<CancellationToken, Task<TResponse?>> call,
        Func<TResponse, (int? Code, string? Msg, T? Data)> project,
        CancellationToken cancellationToken = default,
        Func<object, T?>? recoverFromError = null)
        where TResponse : class
    {
        try
        {
            var response = await call(cancellationToken).ConfigureAwait(false);
            if (response is null)
                return new ApiResult<T>
                {
                    Code = 500,
                    Msg = "服务端返回了空响应",
                    Failure = ApiFailureKind.Server
                };

            var (code, msg, data) = project(response);

            // Any 2xx counts: the envelope's code mirrors the HTTP status, and creation
            // endpoints answer 201 rather than 200. 4xx/5xx codes in the body still mean failure,
            // which is the case this check exists for.
            if (code is null or >= 200 and < 300)
                return new ApiResult<T>
                {
                    IsSuccess = true,
                    Code = code ?? 200,
                    Msg = msg ?? string.Empty,
                    Data = data
                };

            // HTTP 200 但业务码非 200:服务端用响应体而非状态码表达失败。
            return new ApiResult<T>
            {
                Code = code.Value,
                Msg = msg ?? string.Empty,
                Failure = Classify(code.Value)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new ApiResult<T> { Msg = "请求超时", Failure = ApiFailureKind.Network };
        }
        catch (ApiException ex)
        {
            var raw = ExtractRawErrorData(ex);
            return new ApiResult<T>
            {
                Code = ex.ResponseStatusCode,
                Msg = ExtractMsg(ex),
                Failure = Classify(ex.ResponseStatusCode),
                ErrorData = raw,
                Data = raw is not null && recoverFromError is not null ? recoverFromError(raw) : default
            };
        }
        catch (HttpRequestException ex)
        {
            return new ApiResult<T> { Msg = ex.Message, Failure = ApiFailureKind.Network };
        }
        catch (Exception ex)
        {
            return new ApiResult<T> { Msg = ex.Message, Failure = ApiFailureKind.Unknown };
        }
    }

    /// <summary>执行一次无数据返回的调用。</summary>
    public static async Task<ApiResult> RunAsync<TResponse>(
        Func<CancellationToken, Task<TResponse?>> call,
        Func<TResponse, (int? Code, string? Msg)> project,
        CancellationToken cancellationToken = default)
        where TResponse : class
    {
        var result = await RunAsync<TResponse, object>(
            call,
            r =>
            {
                var (code, msg) = project(r);
                return (code, msg, (object?)null);
            },
            cancellationToken).ConfigureAwait(false);

        return new ApiResult
        {
            IsSuccess = result.IsSuccess,
            Code = result.Code,
            Msg = result.Msg,
            Failure = result.Failure,
            ErrorData = result.ErrorData
        };
    }

    /// <summary>把 HTTP 状态码归类。所有端点的 errorMapping 结构一致,因此可以统一处理。</summary>
    private static ApiFailureKind Classify(int status)
    {
        return status switch
        {
            400 => ApiFailureKind.BadRequest,
            401 => ApiFailureKind.Unauthorized,
            403 => ApiFailureKind.Forbidden,
            404 => ApiFailureKind.NotFound,
            >= 500 => ApiFailureKind.Server,
            0 => ApiFailureKind.Network,
            _ => ApiFailureKind.Unknown
        };
    }

    /// <summary>
    ///     取出错误消息。生成的错误类型都带 <c>Msg</c> 属性但没有公共接口,故用反射并缓存访问器。
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "Msg 属性由 LoliaFrpClient.Core/ILLink.Descriptors.xml 显式保留,该文件与本方法是一对,改动其一需同步另一处。")]
    private static string ExtractMsg(ApiException ex)
    {
        var property = MsgProperties.GetOrAdd(ex.GetType(), static type => type.GetProperty("Msg"));
        if (property?.GetValue(ex) is string msg && !string.IsNullOrWhiteSpace(msg)) return msg;

        return ex.Message;
    }

    /// <summary>
    ///     取出失败响应体里的 <c>data</c>。生成的错误类型只反序列化 <c>code</c> 与 <c>msg</c>,
    ///     其余字段落在 <c>AdditionalData</c> 中。
    /// </summary>
    private static object? ExtractRawErrorData(ApiException ex)
    {
        if (ex is IAdditionalDataHolder holder &&
            holder.AdditionalData.TryGetValue("data", out var raw))
            return raw;

        return null;
    }
}
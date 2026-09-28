using LoliaFrpClient.Api.User.Domain;
using DomainVerify = LoliaFrpClient.Api.User.Domain.Verify;

namespace LoliaFrpClient.Core;

/// <summary>
///     自定义域名。
///     <para>
///         这里没有做成实体类,因为列表响应里的域名项没有暴露 <c>domain_id</c>
///         (见 <c>DomainGetResponse_data_domains</c> 的字段),无法从返回值构造出一个带身份的实体。
///         删除操作因此需要调用方自行传入 ID。等 spec 补上该字段后再改成实体。
///     </para>
/// </summary>
public static class Domain
{
    /// <summary>取域名列表。</summary>
    public static Task<ApiResult<DomainGetResponse_data>> ListAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<DomainGetResponse, DomainGetResponse_data>(
            c => api.Client.User.Domain.GetAsDomainGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>添加域名。</summary>
    public static Task<ApiResult<DomainPostResponse_data>> AddAsync(
        string domain,
        string? remark = null,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<DomainPostResponse, DomainPostResponse_data>(
            c => api.Client.User.Domain.PostAsDomainPostResponseAsync(
                new DomainPostRequestBody { Domain = domain, Remark = remark },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>验证域名归属。</summary>
    public static Task<ApiResult<DomainVerify.VerifyPostResponse_data>> VerifyAsync(
        string domain,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<DomainVerify.VerifyPostResponse, DomainVerify.VerifyPostResponse_data>(
            c => api.Client.User.Domain.Verify.PostAsVerifyPostResponseAsync(
                new DomainVerify.VerifyPostRequestBody { Domain = domain },
                cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    /// <summary>删除域名。</summary>
    /// <param name="domainId">域名 ID。列表响应不提供此字段,需由调用方从别处取得。</param>
    /// <param name="session">目标会话。</param>
    /// <param name="cancellationToken">取消标记。</param>
    public static Task<ApiResult> DeleteAsync(
        int domainId,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<Api.User.Domain.Item.WithDomain_DeleteResponse>(
            c => api.Client.User.Domain[domainId].DeleteAsWithDomain_DeleteResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg),
            cancellationToken);
    }
}

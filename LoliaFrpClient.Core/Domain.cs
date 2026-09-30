using LoliaFrpClient.Api.User.Domain;
using DomainVerify = LoliaFrpClient.Api.User.Domain.Verify;

namespace LoliaFrpClient.Core;

public static class Domain
{
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
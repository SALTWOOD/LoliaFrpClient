using LoliaFrpClient.Api.User.Billing.Bills;
using BillItem = LoliaFrpClient.Api.User.Billing.Bills.Item;

namespace LoliaFrpClient.Core;

public sealed class Bill : ApiFacade
{
    public Bill(string orderNo) : this(orderNo, ApiSession.Current)
    {
    }

    public Bill(string orderNo, ApiSession session) : base(session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNo);
        OrderNo = orderNo;
    }

    public string OrderNo { get; }

    public static Task<ApiResult<BillsGetResponse_data>> ListAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<BillsGetResponse, BillsGetResponse_data>(
            c => api.Client.User.Billing.Bills.GetAsBillsGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<BillsPostResponse_data>> CreateAsync(
        BillsPostRequestBody request,
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<BillsPostResponse, BillsPostResponse_data>(
            c => api.Client.User.Billing.Bills.PostAsBillsPostResponseAsync(request, cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public Task<ApiResult<BillItem.WithOrder_noGetResponse_data>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        return ApiCall.RunAsync<BillItem.WithOrder_noGetResponse, BillItem.WithOrder_noGetResponse_data>(
            c => Client.User.Billing.Bills[OrderNo].GetAsWithOrder_noGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }
}
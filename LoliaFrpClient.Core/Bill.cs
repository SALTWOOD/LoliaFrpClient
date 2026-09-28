using LoliaFrpClient.Api.User.Billing.Bills;
using BillItem = LoliaFrpClient.Api.User.Billing.Bills.Item;

namespace LoliaFrpClient.Core;

/// <summary>
///     账单。身份是订单号。
/// </summary>
public sealed class Bill : ApiFacade
{
    /// <summary>用当前会话创建指向指定订单的引用。</summary>
    public Bill(string orderNo) : this(orderNo, ApiSession.Current)
    {
    }

    /// <summary>用指定会话创建指向指定订单的引用。</summary>
    public Bill(string orderNo, ApiSession session) : base(session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNo);
        OrderNo = orderNo;
    }

    /// <summary>订单号。</summary>
    public string OrderNo { get; }

    /// <summary>取账单列表。</summary>
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

    /// <summary>创建支付账单。返回体里含支付页地址。</summary>
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

    /// <summary>取本账单详情。</summary>
    public Task<ApiResult<BillItem.WithOrder_noGetResponse_data>> GetAsync(CancellationToken cancellationToken = default) =>
        ApiCall.RunAsync<BillItem.WithOrder_noGetResponse, BillItem.WithOrder_noGetResponse_data>(
            c => Client.User.Billing.Bills[OrderNo].GetAsWithOrder_noGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
}

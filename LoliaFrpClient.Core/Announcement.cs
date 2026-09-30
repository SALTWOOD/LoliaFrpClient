using LoliaFrpClient.Api.User.Announcements;
using AnnouncementBoard = LoliaFrpClient.Api.User.Announcements.Board;
using AnnouncementModal = LoliaFrpClient.Api.User.Announcements.Modal;
using AnnouncementTop = LoliaFrpClient.Api.User.Announcements.Top;

namespace LoliaFrpClient.Core;

/// <summary>
///     公告
/// </summary>
public static class Announcement
{
    public static Task<ApiResult<AnnouncementsGetResponse_data>> ListAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<AnnouncementsGetResponse, AnnouncementsGetResponse_data>(
            c => api.Client.User.Announcements.GetAsAnnouncementsGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<AnnouncementTop.TopGetResponse_data>> GetTopAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<AnnouncementTop.TopGetResponse, AnnouncementTop.TopGetResponse_data>(
            c => api.Client.User.Announcements.Top.GetAsTopGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<AnnouncementBoard.BoardGetResponse_data>> GetBoardAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<AnnouncementBoard.BoardGetResponse, AnnouncementBoard.BoardGetResponse_data>(
            c => api.Client.User.Announcements.Board.GetAsBoardGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }

    public static Task<ApiResult<AnnouncementModal.ModalGetResponse_data>> GetModalAsync(
        ApiSession? session = null,
        CancellationToken cancellationToken = default)
    {
        var api = session ?? ApiSession.Current;

        return ApiCall.RunAsync<AnnouncementModal.ModalGetResponse, AnnouncementModal.ModalGetResponse_data>(
            c => api.Client.User.Announcements.Modal.GetAsModalGetResponseAsync(cancellationToken: c),
            r => (r.Code, r.Msg, r.Data),
            cancellationToken);
    }
}
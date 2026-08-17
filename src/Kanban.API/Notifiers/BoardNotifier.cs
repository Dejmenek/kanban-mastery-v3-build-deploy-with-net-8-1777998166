using Kanban.API.DTOs.Boards;
using Kanban.API.DTOs.Boards.Cards;
using Kanban.API.DTOs.Boards.Columns;
using Kanban.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Kanban.API.Notifiers;

public interface IBoardNotifier
{
    Task CardCreated(int boardId, int columnId, CardResponse card);
    Task CardMoved(int boardId, MoveCardResponse moveCard);
    Task CardUpdated(int boardId, CardResponse card);
    Task CardAssigned(int boardId, CardResponse card);
    Task CardDeleted(int boardId, int cardId);

    Task ColumnCreated(int boardId, ColumnResponse column);
    Task ColumnUpdated(int boardId, ColumnResponse column);
    Task ColumnMoved(int boardId, MoveColumnResponse moveColumn);
    Task ColumnDeleted(int boardId, int columnId);

    Task MemberAdded(int boardId, BoardMemberResponse member);

    Task BoardUpdated(int boardId, BoardResponse board);
    Task BoardDeleted(int boardId);
}

public interface IBoardClient
{
    Task CardCreated(int columnId, CardResponse card);
    Task CardMoved(MoveCardResponse moveCard);
    Task CardUpdated(CardResponse card);
    Task CardAssigned(CardResponse card);
    Task CardDeleted(int cardId);

    Task ColumnCreated(ColumnResponse column);
    Task ColumnUpdated(ColumnResponse column);
    Task ColumnMoved(MoveColumnResponse moveColumn);
    Task ColumnDeleted(int columnId);

    Task MemberAdded(BoardMemberResponse member);

    Task BoardUpdated(BoardResponse board);
    Task BoardDeleted(int boardId);
}

public class BoardNotifier(
    IHubContext<BoardHub, IBoardClient> boardHubContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<BoardNotifier> logger) : IBoardNotifier
{
    public const string ConnectionIdHeaderName = "X-SignalR-Connection-Id";

    public Task BoardDeleted(int boardId) =>
        SafeInvoke(() => TargetClients(boardId).BoardDeleted(boardId), nameof(BoardDeleted), boardId);

    public Task BoardUpdated(int boardId, BoardResponse board) =>
        SafeInvoke(() => TargetClients(boardId).BoardUpdated(board), nameof(BoardUpdated), boardId);

    public Task CardAssigned(int boardId, CardResponse card) =>
        SafeInvoke(() => TargetClients(boardId).CardAssigned(card), nameof(CardAssigned), boardId);

    public Task CardCreated(int boardId, int columnId, CardResponse card) =>
        SafeInvoke(() => TargetClients(boardId).CardCreated(columnId, card), nameof(CardCreated), boardId);

    public Task CardDeleted(int boardId, int cardId) =>
        SafeInvoke(() => TargetClients(boardId).CardDeleted(cardId), nameof(CardDeleted), boardId);

    public Task CardMoved(int boardId, MoveCardResponse moveCard) =>
        SafeInvoke(() => TargetClients(boardId).CardMoved(moveCard), nameof(CardMoved), boardId);

    public Task CardUpdated(int boardId, CardResponse card) =>
        SafeInvoke(() => TargetClients(boardId).CardUpdated(card), nameof(CardUpdated), boardId);

    public Task ColumnCreated(int boardId, ColumnResponse column) =>
        SafeInvoke(() => TargetClients(boardId).ColumnCreated(column), nameof(ColumnCreated), boardId);

    public Task ColumnDeleted(int boardId, int columnId) =>
        SafeInvoke(() => TargetClients(boardId).ColumnDeleted(columnId), nameof(ColumnDeleted), boardId);

    public Task ColumnMoved(int boardId, MoveColumnResponse moveColumn) =>
        SafeInvoke(() => TargetClients(boardId).ColumnMoved(moveColumn), nameof(ColumnMoved), boardId);

    public Task ColumnUpdated(int boardId, ColumnResponse column) =>
        SafeInvoke(() => TargetClients(boardId).ColumnUpdated(column), nameof(ColumnUpdated), boardId);

    public Task MemberAdded(int boardId, BoardMemberResponse member) =>
        SafeInvoke(() => TargetClients(boardId).MemberAdded(member), nameof(MemberAdded), boardId);

    private IBoardClient TargetClients(int boardId)
    {
        var connectionId = httpContextAccessor.HttpContext?.Request.Headers[ConnectionIdHeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(connectionId))
        {
            logger.LogInformation("Broadcasting to group {Group} (no connection to exclude)", GroupName(boardId));
            return boardHubContext.Clients.Group(GroupName(boardId));
        }

        logger.LogInformation("Broadcasting to group {Group} excluding connection {ConnectionId}", GroupName(boardId), connectionId);
        return boardHubContext.Clients.GroupExcept(GroupName(boardId), [connectionId]);
    }

    private async Task SafeInvoke(Func<Task> send, string eventName, int boardId)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast {Event} for board {BoardId}", eventName, boardId);
        }
    }

    private static string GroupName(int boardId) => $"board-{boardId}";
}
using Kanban.API.Notifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kanban.API.Hubs;

[Authorize]
public sealed class BoardHub(IAuthorizationService authService, ILogger<BoardHub> logger) : Hub<IBoardClient>
{
    public async Task JoinBoard(int boardId)
    {
        var authResult = await authService.AuthorizeAsync(Context.User!, boardId, "IsBoardMember");
        if (!authResult.Succeeded)
        {
            logger.LogInformation(
                "JoinBoard REJECTED: connection {ConnectionId} is not a member of board {BoardId}", Context.ConnectionId, boardId);
            throw new HubException("Forbidden: not a board member.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(boardId));
        logger.LogInformation("JoinBoard OK: connection {ConnectionId} joined group {Group}", Context.ConnectionId, GroupName(boardId));
    }

    public async Task LeaveBoard(int boardId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(boardId));
        logger.LogInformation("LeaveBoard: connection {ConnectionId} left group {Group}", Context.ConnectionId, GroupName(boardId));
    }

    private static string GroupName(int boardId) => $"board-{boardId}";
}

namespace Kanban.API.DTOs.Boards.Cards;

public record CardAssigneeResponse(
    string UserId,
    string? UserName,
    string? Email);

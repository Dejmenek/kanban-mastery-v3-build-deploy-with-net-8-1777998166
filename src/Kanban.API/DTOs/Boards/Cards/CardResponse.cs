namespace Kanban.API.DTOs.Boards.Cards;

public record CardResponse(
    int Id,
    string Title,
    string? Description,
    int Position);

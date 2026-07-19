namespace Kanban.API.DTOs.Boards.Cards;

public record CreateCardRequest(string Title, string? Description, int ColumnId);

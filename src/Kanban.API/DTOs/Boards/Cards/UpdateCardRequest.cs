namespace Kanban.API.DTOs.Boards.Cards;

public record UpdateCardRequest(string Title, string? Description, int ColumnId);

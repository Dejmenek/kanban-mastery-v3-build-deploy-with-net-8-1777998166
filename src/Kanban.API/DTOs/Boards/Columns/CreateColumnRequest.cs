namespace Kanban.API.DTOs.Boards.Columns;

public record CreateColumnRequest(string Title, string? Description, int? Position);

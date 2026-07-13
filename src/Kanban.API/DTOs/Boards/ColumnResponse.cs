namespace Kanban.API.DTOs.Boards;

public record ColumnResponse(
    int Id,
    string Title,
    string? Description,
    int Position,
    IReadOnlyList<CardResponse> Cards);

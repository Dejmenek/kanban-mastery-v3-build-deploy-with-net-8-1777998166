namespace Kanban.API.DTOs.Boards;

public record BoardDetailsResponse(
    int Id,
    string Name,
    string? Description,
    IReadOnlyList<ColumnResponse> Columns);

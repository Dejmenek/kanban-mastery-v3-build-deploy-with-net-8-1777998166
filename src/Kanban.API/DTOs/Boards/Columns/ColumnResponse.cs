using Kanban.API.DTOs.Boards.Cards;

namespace Kanban.API.DTOs.Boards.Columns;

public record ColumnResponse(
    int Id,
    string Title,
    string? Description,
    int Position,
    IReadOnlyList<CardResponse> Cards);

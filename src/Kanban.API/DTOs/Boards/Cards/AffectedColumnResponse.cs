namespace Kanban.API.DTOs.Boards.Cards;

public record AffectedColumnResponse(int ColumnId, IReadOnlyList<CardPositionResponse> Cards);

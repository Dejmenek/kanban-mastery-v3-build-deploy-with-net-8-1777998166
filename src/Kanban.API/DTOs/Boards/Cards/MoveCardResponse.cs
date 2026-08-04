namespace Kanban.API.DTOs.Boards.Cards;

public record MoveCardResponse(
    int CardId,
    int ColumnId,
    int Position,
    IReadOnlyList<AffectedColumnResponse> AffectedColumns);

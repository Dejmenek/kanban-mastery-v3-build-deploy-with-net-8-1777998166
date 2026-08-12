namespace Kanban.API.DTOs.Boards.Columns;

public record MoveColumnResponse(int ColumnId, int Position, IReadOnlyList<ColumnPositionResponse> AffectedColumns);

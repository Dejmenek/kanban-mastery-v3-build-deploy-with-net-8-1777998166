using Kanban.API.Common;

namespace Kanban.API.Errors;

public static class ColumnErrors
{
    public static Error PositionConflict(int boardId) =>
        Error.Conflict("Column.PositionConflict", $"A column with the same position already exists in board '{boardId}'.");

    public static Error InvalidTitle =>
        Error.Validation("Column.InvalidTitle", "Column title cannot be null or empty.");
}

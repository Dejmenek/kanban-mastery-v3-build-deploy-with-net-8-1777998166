using Kanban.API.Common;

namespace Kanban.API.Errors;

public static class ColumnErrors
{
    public static Error PositionConflict(int boardId) =>
        Error.Conflict("Column.PositionConflict", $"A column with the same position already exists in board '{boardId}'.");

    public static Error InvalidTitle =>
        Error.Validation("Column.InvalidTitle", "Column title cannot be null or empty.");

    public static Error NotFound(int columnId) =>
        Error.NotFound("Column.NotFound", $"Column with ID '{columnId}' was not found.");

    public static Error HasCards(int columnId) =>
        Error.Conflict("Column.HasCards", $"Column with ID '{columnId}' cannot be deleted because it contains cards. Please move or delete the cards first.");
}

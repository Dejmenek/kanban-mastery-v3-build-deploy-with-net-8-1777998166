namespace Kanban.API.Common;

public record CursorPagedResponse<T>(IReadOnlyList<T> Items, string? NextCursor);

namespace Kanban.API.DTOs.Boards.Columns;

public record MoveColumnRequest(int TargetPosition, int ExpectedPosition);

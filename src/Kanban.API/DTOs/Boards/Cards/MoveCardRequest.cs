namespace Kanban.API.DTOs.Boards.Cards;

public record MoveCardRequest(
    int TargetColumnId,
    int TargetPosition,
    int ExpectedColumnId,
    int ExpectedPosition);

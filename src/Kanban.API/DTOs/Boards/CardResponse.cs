namespace Kanban.API.DTOs.Boards;

public record CardResponse(
    int Id,
    string Title,
    string? Description,
    int Position);

namespace Kanban.API.DTOs.Boards;

public record BoardSummaryResponse(
    int Id,
    string Name,
    string UserRole);

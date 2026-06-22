namespace Kanban.API.DTOs.Boards;

public record BoardResponse(
    int Id,
    string Name,
    string? Description,
    IReadOnlyList<BoardMemberResponse> Members);

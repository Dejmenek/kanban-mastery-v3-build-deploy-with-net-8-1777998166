namespace Kanban.API.DTOs.Boards;

public record BoardMemberResponse(
    string MemberId,
    string? UserName,
    string Role);

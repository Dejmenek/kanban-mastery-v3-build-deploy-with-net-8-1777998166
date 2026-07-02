namespace Kanban.API.DTOs.Users;

public record CurrentUserProfileResponse(string Id, string? UserName, string? Email);

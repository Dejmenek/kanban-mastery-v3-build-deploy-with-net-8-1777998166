using Kanban.API.Common;

namespace Kanban.API.Errors;

public static class UserErrors
{
    public static Error NotFound(string userId) =>
        Error.NotFound("User.NotFound", $"User with ID '{userId}' was not found.");
}

using Kanban.API.Common;

namespace Kanban.API.Errors;

public static class BoardErrors
{
    public static Error NotFound(int boardId) =>
        Error.NotFound("Board.NotFound", $"Board with id '{boardId}' was not found.");

    public static Error MissingMemberIdentifier =>
        Error.Validation("Board.MissingMemberIdentifier", "Either UserId or Email must be provided.");

    public static Error UserNotFound(string identifier) =>
        Error.NotFound("Board.UserNotFound", $"User '{identifier}' was not found.");

    public static Error AlreadyMember =>
        Error.Conflict("Board.AlreadyMember", "User is already a member of the board.");

    public static Error InvalidName =>
        Error.Validation("Board.InvalidName", "Board name must not be empty.");

    public static Error UserNotMember(string userId, int boardId) =>
        Error.Validation("Board.UserNotMember", $"User with id '{userId}' is not a member of board '{boardId}'.");
}

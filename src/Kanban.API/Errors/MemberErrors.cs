using Kanban.API.Common;

namespace Kanban.API.Errors;

public static class MemberErrors
{
    public static Error InvalidCursor =>
        Error.Validation("Member.InvalidCursor", "The provided cursor is invalid.");

    public static Error InvalidSearchQuery =>
        Error.Validation("Member.InvalidSearchQuery", "Search query must be at least 2 characters long.");
}

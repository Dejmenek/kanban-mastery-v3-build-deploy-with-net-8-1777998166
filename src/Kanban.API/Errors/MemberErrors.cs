using Kanban.API.Common;

namespace Kanban.API.Errors;

public static class MemberErrors
{
    public static Error InvalidCursor =>
        Error.Validation("Member.InvalidCursor", "The provided cursor is invalid.");
}

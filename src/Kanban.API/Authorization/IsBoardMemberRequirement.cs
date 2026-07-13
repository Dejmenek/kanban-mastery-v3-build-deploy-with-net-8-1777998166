using Kanban.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kanban.API.Authorization;

public class IsBoardMemberRequirement : IAuthorizationRequirement { }

public class IsBoardMemberHandler(ApplicationDbContext dbContext) : AuthorizationHandler<IsBoardMemberRequirement, int>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IsBoardMemberRequirement requirement, int boardId)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isMember = await dbContext.BoardsMemberships
            .AsNoTracking()
            .AnyAsync(m => m.BoardId == boardId && m.MemberId == userId);

        if (isMember) context.Succeed(requirement);
    }
}


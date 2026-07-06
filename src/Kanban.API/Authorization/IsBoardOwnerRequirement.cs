using Kanban.API.Data;
using Kanban.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kanban.API.Authorization;

public class IsBoardOwnerRequirement : IAuthorizationRequirement { }

public class IsBoardOwnerHandler(ApplicationDbContext dbContext) : AuthorizationHandler<IsBoardOwnerRequirement, int>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, IsBoardOwnerRequirement requirement, int boardId)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isOwner = await dbContext.BoardsMemberships
            .AsNoTracking()
            .AnyAsync(m => m.BoardId == boardId && m.MemberId == userId && m.Role == Role.Owner);

        if (isOwner) context.Succeed(requirement);
    }
}

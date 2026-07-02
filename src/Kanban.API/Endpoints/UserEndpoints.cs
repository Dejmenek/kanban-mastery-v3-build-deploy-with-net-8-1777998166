using Kanban.API.Data;
using Kanban.API.DTOs.Users;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;

namespace Kanban.API.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users");

        group.MapGet("/me", GetCurrentUserProfile)
            .RequireAuthorization();
    }

    private static async Task<Results<Ok<CurrentUserProfileResponse>, NotFound, UnauthorizedHttpResult>> GetCurrentUserProfile(
        ClaimsPrincipal user, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return TypedResults.Unauthorized();

        var appUser = await db.Users.FindAsync([userId], cancellationToken);

        if (appUser is null) return TypedResults.NotFound();

        return TypedResults.Ok(new CurrentUserProfileResponse(appUser.Id, appUser.UserName, appUser.Email));
    }
}

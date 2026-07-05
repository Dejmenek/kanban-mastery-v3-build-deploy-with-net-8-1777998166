using Kanban.API.Data;
using Kanban.API.DTOs.Users;
using Kanban.API.Services;
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
        ClaimsPrincipal user, ApplicationDbContext db, IUserService userService, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return TypedResults.Unauthorized();

        var result = await userService.GetCurrentUserProfileAsync(userId, cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.NotFound();
    }
}

using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.DTOs.Users;
using Kanban.API.Services;
using System.Security.Claims;

namespace Kanban.API.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users");

        group.MapGet("/me", GetCurrentUserProfile)
            .RequireAuthorization()
            .Produces<CurrentUserProfileResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<string>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetCurrentUserProfile(
        ClaimsPrincipal user, ApplicationDbContext db, IUserService userService, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return TypedResults.Unauthorized();

        var result = await userService.GetCurrentUserProfileAsync(userId, cancellationToken);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : result.Error.ToTypedResult();
    }
}

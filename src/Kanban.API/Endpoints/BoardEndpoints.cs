using Kanban.API.DTOs.Boards;
using Kanban.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;

namespace Kanban.API.Endpoints;

public static class BoardEndpoints
{
    public static void MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/boards")
            .RequireAuthorization();

        group.MapGet("/", GetAllForUser);
        group.MapPost("/", CreateBoard);
    }

    private static async Task<Results<Ok<IReadOnlyList<BoardSummaryResponse>>, UnauthorizedHttpResult>> GetAllForUser(
        IBoardService boardService, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await boardService.GetAllForUserAsync(userId, cancellationToken);
        return TypedResults.Ok(result.Value);
    }

    private static async Task<Results<Created<BoardResponse>, UnauthorizedHttpResult>> CreateBoard(
        CreateBoardRequest request, IBoardService boardService, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await boardService.CreateAsync(request, userId, cancellationToken);

        return TypedResults.Created<BoardResponse>($"/api/boards/{result.Value.Id}", result.Value);
    }
}

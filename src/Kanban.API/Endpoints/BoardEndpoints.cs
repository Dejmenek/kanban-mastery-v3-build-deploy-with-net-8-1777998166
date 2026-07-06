using Kanban.API.Common;
using Kanban.API.DTOs.Boards;
using Kanban.API.Services;
using Microsoft.AspNetCore.Authorization;
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
        group.MapPost("/{boardId:int}/members", AddMember);
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

    private static async Task<Results<Created<BoardMemberResponse>, BadRequest<string>, NotFound<string>, Conflict<string>, ForbidHttpResult, UnauthorizedHttpResult>> AddMember(
        int boardId,
        AddBoardMemberRequest request,
        IAuthorizationService authService,
        IBoardService boardService, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var authResult = await authService.AuthorizeAsync(user, boardId, "IsBoardOwner");
        if (!authResult.Succeeded)
        {
            return TypedResults.Forbid();
        }

        var result = await boardService.AddMemberAsync(boardId, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error.Description),
                ErrorType.Conflict => TypedResults.Conflict(result.Error.Description),
                _ => TypedResults.BadRequest(result.Error.Description)
            };
        }

        return TypedResults.Created<BoardMemberResponse>($"/api/boards/{boardId}/members/{result.Value.MemberId}", result.Value);
    }
}

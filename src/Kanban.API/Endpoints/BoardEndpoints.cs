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
        var boards = app.MapGroup("/api/boards")
            .RequireAuthorization();

        boards.MapGet("/", GetAllForUser);
        boards.MapPost("/", CreateBoard);
        boards.MapPost("/{boardId:int}/members", AddMember)
            .Produces<BoardMemberResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status409Conflict);
        boards.MapGet("/{boardId:int}", GetById)
            .Produces<BoardDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status404NotFound);
        boards.MapPut("/{boardId:int}", UpdateBoard)
            .Produces<BoardResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound);
        boards.MapDelete("/{boardId:int}", DeleteBoard)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden);

        boards.MapColumnEndpoints();
    }

    private static async Task<IResult> GetById(
        int boardId, IBoardService boardService, ClaimsPrincipal user,
        IAuthorizationService authService, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var authResult = await authService.AuthorizeAsync(user, boardId, "IsBoardMember");
        if (!authResult.Succeeded)
        {
            return TypedResults.Forbid();
        }

        var result = await boardService.GetByIdAsync(boardId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToTypedResult();
        }

        return TypedResults.Ok(result.Value);
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

    private static async Task<IResult> AddMember(
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
            return result.Error.ToTypedResult();
        }

        return TypedResults.Created<BoardMemberResponse>($"/api/boards/{boardId}/members/{result.Value.MemberId}", result.Value);
    }

    private static async Task<IResult> UpdateBoard(
        int boardId,
        UpdateBoardRequest request,
        IAuthorizationService authService,
        IBoardService boardService, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var authResult = await authService.AuthorizeAsync(user, boardId, "IsBoardOwner");
        if (!authResult.Succeeded) return TypedResults.Forbid();

        var result = await boardService.UpdateAsync(boardId, request, cancellationToken);
        if (result.IsFailure) return result.Error.ToTypedResult();

        return TypedResults.Ok(result.Value);
    }

    private static async Task<IResult> DeleteBoard(
        int boardId,
        IAuthorizationService authService,
        IBoardService boardService, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var authResult = await authService.AuthorizeAsync(user, boardId, "IsBoardOwner");
        if (!authResult.Succeeded)
        {
            return TypedResults.Forbid();
        }

        await boardService.DeleteAsync(boardId, cancellationToken);

        return TypedResults.NoContent();
    }
}

using Kanban.API.Common;
using Kanban.API.DTOs.Boards;
using Kanban.API.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Kanban.API.Endpoints;

public static class MemberEndpoints
{
    public static void MapMemberEndpoints(this IEndpointRouteBuilder boardsGroup)
    {
        var members = boardsGroup.MapGroup("/{boardId:int}/members")
            .RequireAuthorization();

        members.MapPost("/", AddMember)
            .Produces<BoardMemberResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status409Conflict);
        members.MapGet("/", GetAllMembers)
            .Produces<CursorPagedResponse<BoardMemberResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> AddMember(
        int boardId,
        AddBoardMemberRequest request,
        IAuthorizationService authService,
        IMemberService memberService, ClaimsPrincipal user, CancellationToken cancellationToken)
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

        var result = await memberService.AddMemberAsync(boardId, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToTypedResult();
        }

        return TypedResults.Created<BoardMemberResponse>($"/api/boards/{boardId}/members/{result.Value.MemberId}", result.Value);
    }

    private static async Task<IResult> GetAllMembers(
        int boardId,
        string? cursor,
        IAuthorizationService authService,
        IMemberService memberService, ClaimsPrincipal user, CancellationToken cancellationToken,
        int pageSize = 20)
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

        var result = await memberService.GetAllAsync(boardId, cursor, pageSize, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToTypedResult();
        }

        return TypedResults.Ok(result.Value);
    }
}

using Kanban.API.Common;
using Kanban.API.DTOs.Boards.Columns;
using Kanban.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;

namespace Kanban.API.Endpoints;

public static class ColumnEndpoints
{
    public static void MapColumnEndpoints(this IEndpointRouteBuilder boardsGroup)
    {
        var columns = boardsGroup.MapGroup("/{boardId:int}/columns")
            .RequireAuthorization();

        columns.MapPost("/", CreateColumn)
            .Produces<ColumnResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        columns.MapPut("/{columnId:int}", UpdateColumn)
            .Produces<ColumnResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        columns.MapDelete("/{columnId:int}", DeleteColumn)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<string>(StatusCodes.Status404NotFound)
            .Produces<string>(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> CreateColumn(
        int boardId, CreateColumnRequest request, IColumnService columnService,
        IAuthorizationService authService, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var authResult = await authService.AuthorizeAsync(user, boardId, "IsBoardMember");
        if (!authResult.Succeeded)
        {
            return TypedResults.Forbid();
        }

        var result = await columnService.CreateAsync(boardId, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToTypedResult();
        }

        return TypedResults.Created($"/api/boards/{boardId}/columns/{result.Value.Id}", result.Value);
    }

    private static async Task<IResult> DeleteColumn(
        int boardId, int columnId, IColumnService columnService, IAuthorizationService authService,
        ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var authResult = await authService.AuthorizeAsync(user, boardId, "IsBoardMember");
        if (!authResult.Succeeded)
        {
            return TypedResults.Forbid();
        }

        var result = await columnService.DeleteAsync(boardId, columnId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToTypedResult();
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> UpdateColumn(
        int boardId, int columnId, UpdateColumnRequest request, IColumnService columnService,
        IAuthorizationService authService, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var authResult = await authService.AuthorizeAsync(user, boardId, "IsBoardMember");
        if (!authResult.Succeeded)
        {
            return TypedResults.Forbid();
        }

        var result = await columnService.UpdateAsync(boardId, columnId, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToTypedResult();
        }

        return TypedResults.Ok(result.Value);
    }
}

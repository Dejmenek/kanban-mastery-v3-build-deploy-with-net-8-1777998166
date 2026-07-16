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

        columns.MapPost("/", CreateColumn);
        columns.MapPut("/{columnId:int}", UpdateColumn);
        columns.MapDelete("/{columnId:int}", DeleteColumn);
    }

    private static async Task<Results<Created<ColumnResponse>, BadRequest<string>, Conflict<string>, NotFound<string>, ForbidHttpResult>> CreateColumn(
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
            return result.Error.Type switch
            {
                ErrorType.Validation => TypedResults.BadRequest(result.Error.Description),
                ErrorType.Conflict => TypedResults.Conflict(result.Error.Description),
                ErrorType.NotFound => TypedResults.NotFound(result.Error.Description),
                _ => TypedResults.BadRequest(result.Error.Description)
            };
        }

        return TypedResults.Created($"/api/boards/{boardId}/columns/{result.Value.Id}", result.Value);
    }

    private static async Task<Results<NoContent, NotFound<string>, Conflict<string>, ProblemHttpResult, ForbidHttpResult>> DeleteColumn(
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
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error.Description),
                ErrorType.Conflict => TypedResults.Conflict(result.Error.Description),
                _ => TypedResults.Problem(
                    detail: result.Error.Description,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Unhandled error type")
            };
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ColumnResponse>, NotFound<string>, BadRequest<string>, ForbidHttpResult>> UpdateColumn(
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
            return result.Error.Type switch
            {
                ErrorType.Validation => TypedResults.BadRequest(result.Error.Description),
                ErrorType.NotFound => TypedResults.NotFound(result.Error.Description),
                _ => TypedResults.BadRequest(result.Error.Description)
            };
        }

        return TypedResults.Ok(result.Value);
    }
}

using Kanban.API.DTOs.Boards.Columns;
using Kanban.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;

namespace Kanban.API.Endpoints;

public static class ColumnEndpoints
{
    public static void MapColumnEndpoints(this IEndpointRouteBuilder boardsGroup)
    {
        var columns = boardsGroup.MapGroup("/{boardId:int}/columns")
            .RequireAuthorization("IsBoardMember");

        columns.MapPost("/", CreateColumn);
        columns.MapPut("/{columnId:int}", UpdateColumn);
        columns.MapDelete("/{columnId:int}", DeleteColumn);
    }

    private static async Task<Results<Created<ColumnResponse>, BadRequest<string>, Conflict<string>, NotFound<string>, UnauthorizedHttpResult>> CreateColumn(
        int boardId, CreateColumnRequest request, IColumnService columnService,
        ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private static async Task<Results<NoContent, NotFound<string>, Conflict<string>, UnauthorizedHttpResult>> DeleteColumn(
        int boardId, int columnId, IColumnService columnService, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private static async Task<Results<Ok<ColumnResponse>, NotFound<string>, BadRequest<string>, Conflict<string>, UnauthorizedHttpResult>> UpdateColumn(
        int boardId, int columnId, UpdateColumnRequest request, IColumnService columnService,
        ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.DTOs.Boards.Columns;
using Kanban.API.Errors;
using Kanban.API.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.Services;

public class ColumnService(ApplicationDbContext context, IRetryExecutor retryExecutor) : IColumnService
{
    private const int MaxAttempts = 3;

    public async Task<Result<ColumnResponse>> CreateAsync(int boardId, CreateColumnRequest request, CancellationToken cancellationToken = default)
    {
        return await retryExecutor.ExecuteAsync(
            maxAttempts: MaxAttempts,
            operation: () => TryCreateAsync(boardId, request, cancellationToken),
            isRetryable: IsPositionConflict,
            onExhausted: () => Result.Failure<ColumnResponse>(ColumnErrors.PositionConflict(boardId)),
            cancellationToken: cancellationToken
        );
    }

    private async Task<Result<ColumnResponse>> TryCreateAsync(
        int boardId, CreateColumnRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return Result.Failure<ColumnResponse>(ColumnErrors.InvalidTitle);

        var boardExists = await context.Boards.AnyAsync(b => b.Id == boardId, cancellationToken);
        if (!boardExists) return Result.Failure<ColumnResponse>(BoardErrors.NotFound(boardId));

        var count = await context.Columns.CountAsync(c => c.BoardId == boardId, cancellationToken);

        var targetPosition = ResolveTargetPosition(request.Position, count);

        if (targetPosition <= count)
        {
            await ShiftColumnsFromAsync(boardId, targetPosition, cancellationToken);
        }

        var newColumn = new Column
        {
            Title = request.Title,
            Description = request.Description,
            Position = targetPosition,
            BoardId = boardId
        };

        context.Columns.Add(newColumn);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new ColumnResponse
        (
            newColumn.Id,
            newColumn.Title,
            newColumn.Description,
            newColumn.Position,
            []
        ));
    }

    public Task<Result> DeleteAsync(int boardId, int columnId, CancellationToken cancellation = default)
    {
        throw new NotImplementedException();
    }

    public Task<Result<ColumnResponse>> UpdateAsync(int boardId, int columnId, UpdateColumnRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private static bool IsPositionConflict(DbUpdateException ex) =>
        ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19 &&
        sqliteEx.Message.Contains("IX_Columns_BoardId_Position");

    private static int ResolveTargetPosition(int? requestedPosition, int existingCount)
    {
        var hasValidPosition = requestedPosition is int position
            && position >= 1
            && position <= existingCount + 1;

        return hasValidPosition ? requestedPosition!.Value : existingCount + 1;
    }

    private async Task ShiftColumnsFromAsync(int boardId, int fromPosition, CancellationToken cancellationToken)
    {
        var columnsToShift = await context.Columns
            .Where(c => c.BoardId == boardId && c.Position >= fromPosition)
            .ToListAsync(cancellationToken);

        foreach (var column in columnsToShift)
        {
            column.Position += 1;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

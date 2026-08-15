using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.DTOs.Boards.Columns;
using Kanban.API.Errors;
using Kanban.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.Services;

public class ColumnService(ApplicationDbContext context, IRetryExecutor retryExecutor) : IColumnService
{
    private const int MaxAttempts = 3;
    private static readonly string[] PositionIndexScopeHints = ["Columns.BoardId, Columns.Position", "IX_Columns_BoardId_Position"];

    public async Task<Result<ColumnResponse>> CreateAsync(int boardId, CreateColumnRequest request, CancellationToken cancellationToken = default)
    {
        return await retryExecutor.ExecuteAsync(
            maxAttempts: MaxAttempts,
            operation: () => TryCreateAsync(boardId, request, cancellationToken),
            isRetryable: ex => DbConflictClassifier.IsRetryableConflict(ex, PositionIndexScopeHints),
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

        var targetPosition = PositionResolver.Resolve(request.Position, count);

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

    public async Task<Result> DeleteAsync(int boardId, int columnId, CancellationToken cancellationToken = default)
    {
        try
        {
            var column = await context.Columns.FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId, cancellationToken);

            if (column is null) return Result.Failure(ColumnErrors.NotFound(columnId));

            var position = column.Position;

            context.Columns.Remove(column);
            await context.SaveChangesAsync(cancellationToken);

            var siblingsToShift = await context.Columns
                .Where(c => c.BoardId == boardId && c.Position > position)
                .OrderBy(c => c.Position)
                .ToListAsync(cancellationToken);
            foreach (var sibling in siblingsToShift) sibling.Position -= 1;

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateException ex) when (DbConflictClassifier.IsForeignKeyViolation(ex))
        {
            return Result.Failure(ColumnErrors.HasCards(columnId));
        }
    }

    public async Task<Result<ColumnResponse>> UpdateAsync(int boardId, int columnId, UpdateColumnRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return Result.Failure<ColumnResponse>(ColumnErrors.InvalidTitle);

        var column = await context.Columns.FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId, cancellationToken);

        if (column is null) return Result.Failure<ColumnResponse>(ColumnErrors.NotFound(columnId));

        column.Title = request.Title;
        column.Description = request.Description;
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new ColumnResponse
        (
            column.Id,
            column.Title,
            column.Description,
            column.Position,
            []
        ));
    }

    public async Task<Result<MoveColumnResponse>> MoveAsync(int boardId, int columnId, MoveColumnRequest request, CancellationToken cancellationToken = default)
    {
        return await retryExecutor.ExecuteAsync(
            maxAttempts: MaxAttempts,
            operation: () => TryMoveAsync(boardId, columnId, request, cancellationToken),
            isRetryable: ex => DbConflictClassifier.IsRetryableConflict(ex, PositionIndexScopeHints),
            onExhausted: () => Result.Failure<MoveColumnResponse>(ColumnErrors.PositionConflict(boardId)),
            cancellationToken: cancellationToken
        );
    }

    private async Task<Result<MoveColumnResponse>> TryMoveAsync(int boardId, int columnId, MoveColumnRequest request, CancellationToken cancellationToken)
    {
        var column = await context.Columns.FirstOrDefaultAsync(c => c.Id == columnId && c.BoardId == boardId, cancellationToken);
        if (column is null) return Result.Failure<MoveColumnResponse>(ColumnErrors.NotFound(columnId));

        if (column.Position != request.ExpectedPosition)
            return Result.Failure<MoveColumnResponse>(ColumnErrors.MoveConflict(columnId));

        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            await ReorderAsync(column, request.TargetPosition, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        var affectedColumns = await GetAffectedColumnsAsync(boardId, cancellationToken);

        return Result.Success(new MoveColumnResponse(column.Id, column.Position, affectedColumns));
    }

    private async Task ReorderAsync(Column column, int requestedPosition, CancellationToken cancellationToken)
    {
        var countInBoard = await context.Columns.CountAsync(c => c.BoardId == column.BoardId, cancellationToken);
        var targetPosition = PositionResolver.Resolve(requestedPosition, countInBoard - 1);
        var oldPosition = column.Position;

        if (targetPosition == oldPosition) return;

        column.Position = -column.Id;
        await context.SaveChangesAsync(cancellationToken);

        if (targetPosition > oldPosition)
        {
            var toShiftDown = await context.Columns
                .Where(c => c.BoardId == column.BoardId && c.Id != column.Id && c.Position > oldPosition && c.Position <= targetPosition)
                .OrderBy(c => c.Position)
                .ToListAsync(cancellationToken);
            foreach (var sibling in toShiftDown) sibling.Position -= 1;
        }
        else
        {
            var toShiftUp = await context.Columns
                .Where(c => c.BoardId == column.BoardId && c.Id != column.Id && c.Position >= targetPosition && c.Position < oldPosition)
                .OrderByDescending(c => c.Position)
                .ToListAsync(cancellationToken);
            foreach (var sibling in toShiftUp) sibling.Position += 1;
        }

        column.Position = targetPosition;
    }

    private async Task<IReadOnlyList<ColumnPositionResponse>> GetAffectedColumnsAsync(int boardId, CancellationToken cancellationToken)
    {
        return await context.Columns
            .Where(c => c.BoardId == boardId)
            .OrderBy(c => c.Position)
            .Select(c => new ColumnPositionResponse(c.Id, c.Position))
            .ToListAsync(cancellationToken);
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

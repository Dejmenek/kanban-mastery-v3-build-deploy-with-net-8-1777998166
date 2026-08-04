using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.DTOs.Boards.Cards;
using Kanban.API.Errors;
using Kanban.API.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.Services;

public class CardService(ApplicationDbContext context, IRetryExecutor retryExecutor) : ICardService
{
    private const int MaxAttempts = 3;

    public async Task<Result<CardResponse>> AssignCardToUserAsync(
        int cardId, int boardId, AssignCardRequest request, CancellationToken cancellationToken)
    {
        var isMember = await context.BoardsMemberships.AnyAsync(bm => bm.BoardId == boardId && bm.MemberId == request.UserId, cancellationToken);
        if (!isMember) return Result.Failure<CardResponse>(BoardErrors.UserNotMember(request.UserId, boardId));

        var card = await context.Cards.FirstOrDefaultAsync(c => c.Id == cardId && c.Column.BoardId == boardId, cancellationToken);
        if (card is null) return Result.Failure<CardResponse>(CardErrors.NotFound(cardId));

        var assignedUser = await context.Users
            .Where(u => u.Id == request.UserId)
            .Select(u => new { u.Id, u.UserName, u.Email })
            .FirstAsync(cancellationToken);

        card.AssignedToUserId = request.UserId;
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new CardResponse
        (
            card.Id,
            card.Title,
            card.Description,
            card.Position,
            new CardAssigneeResponse(assignedUser.Id, assignedUser.UserName, assignedUser.Email)
        ));
    }

    public async Task<Result<CardResponse>> CreateAsync(int boardId, CreateCardRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return Result.Failure<CardResponse>(CardErrors.InvalidTitle);

        var columnExists = await context.Columns.AnyAsync(c => c.Id == request.ColumnId && c.BoardId == boardId, cancellationToken);
        if (!columnExists) return Result.Failure<CardResponse>(ColumnErrors.NotFound(request.ColumnId));

        var count = await context.Cards.CountAsync(c => c.ColumnId == request.ColumnId, cancellationToken);

        var newCard = new Card
        {
            Title = request.Title,
            Description = request.Description,
            ColumnId = request.ColumnId,
            Position = count + 1
        };

        context.Cards.Add(newCard);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new CardResponse
        (
            newCard.Id,
            newCard.Title,
            newCard.Description,
            newCard.Position,
            null
        ));
    }

    public async Task<Result> DeleteAsync(int boardId, int cardId, CancellationToken cancellationToken)
    {
        var card = await context.Cards
            .FirstOrDefaultAsync(c => c.Id == cardId && c.Column.BoardId == boardId, cancellationToken);

        if (card is null) return Result.Failure(CardErrors.NotFound(cardId));

        context.Cards.Remove(card);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<CardResponse>> UpdateAsync(int boardId, int cardId, UpdateCardRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return Result.Failure<CardResponse>(CardErrors.InvalidTitle);

        var card = await context.Cards
            .Include(c => c.AssignedToUser)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.Column.BoardId == boardId, cancellationToken);

        if (card is null) return Result.Failure<CardResponse>(CardErrors.NotFound(cardId));

        card.Title = request.Title;
        card.Description = request.Description;
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new CardResponse
        (
            card.Id,
            card.Title,
            card.Description,
            card.Position,
            card.AssignedToUser is null ? null : new CardAssigneeResponse(
                card.AssignedToUser.Id, card.AssignedToUser.UserName, card.AssignedToUser.Email)
        ));
    }

    public async Task<Result<MoveCardResponse>> MoveAsync(int boardId, int cardId, MoveCardRequest request, CancellationToken cancellationToken)
    {
        return await retryExecutor.ExecuteAsync(
            maxAttempts: MaxAttempts,
            operation: () => TryMoveAsync(boardId, cardId, request, cancellationToken),
            isRetryable: IsRetryableConflict,
            onExhausted: () => Result.Failure<MoveCardResponse>(CardErrors.PositionConflict(request.TargetColumnId)),
            cancellationToken: cancellationToken
        );
    }

    private async Task<Result<MoveCardResponse>> TryMoveAsync(int boardId, int cardId, MoveCardRequest request, CancellationToken cancellationToken)
    {
        var card = await context.Cards.FirstOrDefaultAsync(c => c.Id == cardId && c.Column.BoardId == boardId, cancellationToken);
        if (card is null) return Result.Failure<MoveCardResponse>(CardErrors.NotFound(cardId));

        if (card.ColumnId != request.ExpectedColumnId || card.Position != request.ExpectedPosition)
            return Result.Failure<MoveCardResponse>(CardErrors.MoveConflict(cardId));

        var isMovingColumns = card.ColumnId != request.TargetColumnId;

        if (isMovingColumns)
        {
            var columnExists = await context.Columns.AnyAsync(c => c.Id == request.TargetColumnId && c.BoardId == boardId, cancellationToken);
            if (!columnExists) return Result.Failure<MoveCardResponse>(ColumnErrors.NotFound(request.TargetColumnId));
        }

        var sourceColumnId = card.ColumnId;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        if (isMovingColumns)
        {
            await MoveToAnotherColumnAsync(card, request.TargetColumnId, request.TargetPosition, cancellationToken);
        }
        else
        {
            await ReorderWithinColumnAsync(card, request.TargetPosition, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var affectedColumnIds = isMovingColumns
            ? new[] { sourceColumnId, card.ColumnId }
            : new[] { card.ColumnId };
        var affectedColumns = await GetAffectedColumnsAsync(affectedColumnIds, cancellationToken);

        return Result.Success(new MoveCardResponse(card.Id, card.ColumnId, card.Position, affectedColumns));
    }

    private async Task<IReadOnlyList<AffectedColumnResponse>> GetAffectedColumnsAsync(
        IReadOnlyCollection<int> columnIds, CancellationToken cancellationToken)
    {
        return await context.Columns
            .Where(c => columnIds.Contains(c.Id))
            .Select(c => new AffectedColumnResponse(
                c.Id,
                c.Cards
                    .OrderBy(ca => ca.Position)
                    .Select(ca => new CardPositionResponse(ca.Id, ca.Position))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    private async Task ReorderWithinColumnAsync(Card card, int requestedPosition, CancellationToken cancellationToken)
    {
        var countInColumn = await context.Cards.CountAsync(c => c.ColumnId == card.ColumnId, cancellationToken);
        var targetPosition = PositionResolver.Resolve(requestedPosition, countInColumn - 1);
        var oldPosition = card.Position;

        if (targetPosition == oldPosition) return;

        card.Position = -card.Id;
        await context.SaveChangesAsync(cancellationToken);

        if (targetPosition > oldPosition)
        {
            var toShiftDown = await context.Cards
                .Where(c => c.ColumnId == card.ColumnId && c.Id != card.Id && c.Position > oldPosition && c.Position <= targetPosition)
                .ToListAsync(cancellationToken);
            foreach (var sibling in toShiftDown) sibling.Position -= 1;
        }
        else
        {
            var toShiftUp = await context.Cards
                .Where(c => c.ColumnId == card.ColumnId && c.Id != card.Id && c.Position >= targetPosition && c.Position < oldPosition)
                .ToListAsync(cancellationToken);
            foreach (var sibling in toShiftUp) sibling.Position += 1;
        }

        card.Position = targetPosition;
    }

    private async Task MoveToAnotherColumnAsync(Card card, int newColumnId, int? requestedPosition, CancellationToken cancellationToken)
    {
        var oldColumnId = card.ColumnId;
        var oldPosition = card.Position;

        var sourceSiblings = await context.Cards
            .Where(c => c.ColumnId == oldColumnId && c.Position > oldPosition)
            .ToListAsync(cancellationToken);
        foreach (var sibling in sourceSiblings) sibling.Position -= 1;

        var destinationCount = await context.Cards.CountAsync(c => c.ColumnId == newColumnId, cancellationToken);
        var targetPosition = PositionResolver.Resolve(requestedPosition, destinationCount);

        if (targetPosition <= destinationCount)
        {
            var destinationSiblings = await context.Cards
                .Where(c => c.ColumnId == newColumnId && c.Position >= targetPosition)
                .ToListAsync(cancellationToken);
            foreach (var sibling in destinationSiblings) sibling.Position += 1;
        }

        card.ColumnId = newColumnId;
        card.Position = targetPosition;
    }

    private static bool IsRetryableConflict(DbUpdateException ex) =>
        ex.InnerException is SqliteException sqliteEx &&
        ((sqliteEx.SqliteErrorCode == 19 && sqliteEx.Message.Contains("IX_Cards_ColumnId_Position")) || sqliteEx.SqliteErrorCode is 5 or 6);
}

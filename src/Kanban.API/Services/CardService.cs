using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.DTOs.Boards.Cards;
using Kanban.API.Errors;
using Kanban.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.Services;

public class CardService(ApplicationDbContext context) : ICardService
{
    public async Task<Result<CardResponse>> AssignCardToUserAsync(
        int cardId, int boardId, AssignCardRequest request, CancellationToken cancellationToken)
    {
        var isMember = await context.BoardsMemberships.AnyAsync(bm => bm.BoardId == boardId && bm.MemberId == request.UserId, cancellationToken);
        if (!isMember) return Result.Failure<CardResponse>(BoardErrors.UserNotMember(request.UserId, boardId));

        var card = await context.Cards.FirstOrDefaultAsync(c => c.Id == cardId && c.Column.BoardId == boardId, cancellationToken);
        if (card is null) return Result.Failure<CardResponse>(CardErrors.NotFound(cardId));

        card.AssignedToUserId = request.UserId;
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new CardResponse
        (
            card.Id,
            card.Title,
            card.Description,
            card.Position
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
            newCard.Position
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
            .FirstOrDefaultAsync(c => c.Id == cardId && c.Column.BoardId == boardId, cancellationToken);

        if (card is null) return Result.Failure<CardResponse>(CardErrors.NotFound(cardId));

        if (card.ColumnId != request.ColumnId)
        {
            var columnExists = await context.Columns.AnyAsync(c => c.Id == request.ColumnId && c.BoardId == boardId, cancellationToken);
            if (!columnExists) return Result.Failure<CardResponse>(ColumnErrors.NotFound(request.ColumnId));
        }

        card.Title = request.Title;
        card.Description = request.Description;
        card.ColumnId = request.ColumnId;
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new CardResponse
        (
            card.Id,
            card.Title,
            card.Description,
            card.Position
        ));
    }
}

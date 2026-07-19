using Kanban.API.Common;
using Kanban.API.DTOs.Boards.Cards;

namespace Kanban.API.Services;

public interface ICardService
{
    Task<Result<CardResponse>> CreateAsync(int boardId, CreateCardRequest request, CancellationToken cancellationToken);
    Task<Result<CardResponse>> UpdateAsync(int boardId, int cardId, UpdateCardRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(int boardId, int cardId, CancellationToken cancellationToken);
}

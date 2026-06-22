using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.DTOs.Boards;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.Services;

public class BoardService(ApplicationDbContext context) : IBoardService
{
    public async Task<Result<IReadOnlyList<BoardSummaryResponse>>> GetAllForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var boards = await context.BoardsMemberships
            .Where(bm => bm.MemberId == userId)
            .Select(bm => new BoardSummaryResponse(
                bm.BoardId,
                bm.Board.Name,
                bm.Role.ToString()))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BoardSummaryResponse>>(boards);
    }

    public Task<Result<BoardResponse>> GetByIdAsync(int boardId, string userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Result<BoardResponse>> CreateAsync(CreateBoardRequest request, string userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Result<BoardResponse>> UpdateAsync(int boardId, UpdateBoardRequest request, string userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Result> DeleteAsync(int boardId, string userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

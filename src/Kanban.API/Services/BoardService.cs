using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.DTOs.Boards;
using Kanban.API.Models;
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

    public async Task<Result<BoardResponse>> CreateAsync(
        CreateBoardRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var board = new Board { Name = request.Name, Description = request.Description };
        var membership = new BoardMember { Board = board, MemberId = userId, Role = Role.Owner };

        context.Boards.Add(board);
        context.BoardsMemberships.Add(membership);
        await context.SaveChangesAsync(cancellationToken);

        var userName = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync(cancellationToken);

        return new BoardResponse(board.Id, board.Name, board.Description,
        [
            new BoardMemberResponse(userId, userName, Role.Owner.ToString())
        ]);
    }

    public Task<Result<BoardResponse>> UpdateAsync(int boardId, UpdateBoardRequest request, string userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Result> DeleteAsync(int boardId, string userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

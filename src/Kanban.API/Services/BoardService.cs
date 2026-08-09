using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.DTOs.Boards;
using Kanban.API.DTOs.Boards.Cards;
using Kanban.API.DTOs.Boards.Columns;
using Kanban.API.Errors;
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

    public async Task<Result<BoardDetailsResponse>> GetByIdAsync(int boardId, string userId, CancellationToken cancellationToken = default)
    {
        var board = await context.Boards
            .Where(b => b.Id == boardId)
            .AsSplitQuery()
            .Select(b => new BoardDetailsResponse(
                b.Id,
                b.Name,
                b.Description,
                b.Members
                    .Where(m => m.MemberId == userId)
                    .Select(m => m.Role.ToString())
                    .FirstOrDefault(),
                b.Members
                    .Select(m => new BoardMemberResponse(m.MemberId, m.Member.UserName, m.Member.Email, m.Role.ToString()))
                    .ToList(),
                b.Columns
                    .OrderBy(c => c.Position)
                    .Select(c => new ColumnResponse(
                        c.Id,
                        c.Title,
                        c.Description,
                        c.Position,
                        c.Cards
                            .OrderBy(ca => ca.Position)
                            .Select(ca => new CardResponse(
                            ca.Id,
                            ca.Title,
                            ca.Description,
                            ca.Position,
                            ca.AssignedToUser == null ? null : new CardAssigneeResponse(
                                ca.AssignedToUser.Id, ca.AssignedToUser.UserName, ca.AssignedToUser.Email)))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        if (board is null) return Result.Failure<BoardDetailsResponse>(BoardErrors.NotFound(boardId));

        return board;
    }

    public async Task<Result<BoardSummaryResponse>> CreateAsync(
        CreateBoardRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var board = new Board { Name = request.Name, Description = request.Description };
        var membership = new BoardMember { Board = board, MemberId = userId, Role = Role.Owner };

        context.Boards.Add(board);
        context.BoardsMemberships.Add(membership);
        await context.SaveChangesAsync(cancellationToken);

        return new BoardSummaryResponse(board.Id, board.Name, Role.Owner.ToString());
    }

    public async Task<Result<BoardResponse>> UpdateAsync(
        int boardId, UpdateBoardRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<BoardResponse>(BoardErrors.InvalidName);
        }

        var board = await context.Boards
            .FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);

        if (board is null)
        {
            return Result.Failure<BoardResponse>(BoardErrors.NotFound(boardId));
        }

        board.Name = request.Name;
        board.Description = request.Description;
        await context.SaveChangesAsync(cancellationToken);

        return new BoardResponse(board.Id, board.Name, board.Description);
    }

    public async Task<Result> DeleteAsync(int boardId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.Cards
            .Where(c => c.Column.BoardId == boardId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Boards
            .Where(b => b.Id == boardId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}

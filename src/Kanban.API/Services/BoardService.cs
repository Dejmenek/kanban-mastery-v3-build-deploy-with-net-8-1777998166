using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.DTOs.Boards;
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

    public async Task<Result<BoardDetailsResponse>> GetByIdAsync(int boardId, CancellationToken cancellationToken = default)
    {
        var board = await context.Boards
            .Where(b => b.Id == boardId)
            .Select(b => new BoardDetailsResponse(
                b.Id,
                b.Name,
                b.Description,
                b.Columns
                    .OrderBy(c => c.Position)
                    .Select(c => new ColumnResponse(
                        c.Id,
                        c.Title,
                        c.Description,
                        c.Position,
                        c.Cards
                            .OrderBy(ca => ca.Position)
                            .Select(ca => new CardResponse(ca.Id, ca.Title, ca.Description, ca.Position))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        if (board is null) return Result.Failure<BoardDetailsResponse>(BoardErrors.NotFound(boardId));

        return board;
    }

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

    public async Task<Result<BoardMemberResponse>> AddMemberAsync(int boardId, AddBoardMemberRequest request, CancellationToken cancellationToken = default)
    {
        if (request.UserId is null && request.Email is null)
        {
            return Result.Failure<BoardMemberResponse>(BoardErrors.MissingMemberIdentifier);
        }

        var board = await context.Boards
             .AsNoTracking()
             .Include(b => b.Members)
             .FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardMemberResponse>(BoardErrors.NotFound(boardId));
        }

        var userToAdd = await context.Users
            .AsNoTracking()
            .Where(u => (request.UserId != null && u.Id == request.UserId) ||
                        (request.Email != null && u.Email == request.Email))
            .FirstOrDefaultAsync(cancellationToken);
        if (userToAdd is null)
        {
            return Result.Failure<BoardMemberResponse>(BoardErrors.UserNotFound(request.UserId ?? request.Email!));
        }

        if (board.Members.Any(m => m.MemberId == userToAdd.Id))
        {
            return Result.Failure<BoardMemberResponse>(BoardErrors.AlreadyMember);
        }

        var newMember = new BoardMember
        {
            BoardId = boardId,
            MemberId = userToAdd.Id,
            Role = Role.Member
        };
        context.BoardsMemberships.Add(newMember);
        await context.SaveChangesAsync(cancellationToken);

        return new BoardMemberResponse(userToAdd.Id, userToAdd.UserName, newMember.Role.ToString());
    }
}

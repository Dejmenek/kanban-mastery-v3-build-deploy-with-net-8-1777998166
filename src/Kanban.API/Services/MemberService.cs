using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.DTOs.Boards;
using Kanban.API.Errors;
using Kanban.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.Services;

public class MemberService(ApplicationDbContext context) : IMemberService
{
    private const int MaxPageSize = 100;

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

        return new BoardMemberResponse(userToAdd.Id, userToAdd.UserName, userToAdd.Email, newMember.Role.ToString());
    }

    public async Task<Result<CursorPagedResponse<BoardMemberResponse>>> GetAllAsync(
        int boardId, string? cursor, int pageSize, CancellationToken cancellationToken = default)
    {
        var boardExists = await context.Boards
            .AsNoTracking()
            .AnyAsync(b => b.Id == boardId, cancellationToken);
        if (!boardExists)
        {
            return Result.Failure<CursorPagedResponse<BoardMemberResponse>>(BoardErrors.NotFound(boardId));
        }

        string? decodedCursor = null;
        if (!string.IsNullOrEmpty(cursor))
        {
            if (!CursorEncoder.TryDecode(cursor, out decodedCursor))
            {
                return Result.Failure<CursorPagedResponse<BoardMemberResponse>>(MemberErrors.InvalidCursor);
            }
        }

        var effectivePageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = context.BoardsMemberships
            .AsNoTracking()
            .Where(m => m.BoardId == boardId);

        if (decodedCursor is not null)
        {
            query = query.Where(m => m.MemberId.CompareTo(decodedCursor) > 0);
        }

        var members = await query
            .OrderBy(m => m.MemberId)
            .Take(effectivePageSize + 1)
            .Select(m => new BoardMemberResponse(m.MemberId, m.Member.UserName, m.Member.Email, m.Role.ToString()))
            .ToListAsync(cancellationToken);

        var hasMore = members.Count > effectivePageSize;
        if (hasMore)
        {
            members.RemoveAt(members.Count - 1);
        }

        var nextCursor = hasMore ? CursorEncoder.Encode(members[^1].MemberId) : null;

        return new CursorPagedResponse<BoardMemberResponse>(members, nextCursor);
    }
}

using Kanban.API.Common;
using Kanban.API.DTOs.Boards;

namespace Kanban.API.Services;

public interface IMemberService
{
    Task<Result<BoardMemberResponse>> AddMemberAsync(int boardId, AddBoardMemberRequest request, CancellationToken cancellationToken = default);

    Task<Result<CursorPagedResponse<BoardMemberResponse>>> GetAllAsync(int boardId, string? cursor, int pageSize, CancellationToken cancellationToken = default);
}

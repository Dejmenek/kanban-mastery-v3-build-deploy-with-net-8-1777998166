using Kanban.API.Common;
using Kanban.API.DTOs.Boards;

namespace Kanban.API.Services;

public interface IBoardService
{
    Task<Result<BoardDetailsResponse>> GetByIdAsync(int boardId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BoardSummaryResponse>>> GetAllForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<Result<BoardResponse>> CreateAsync(CreateBoardRequest request, string userId, CancellationToken cancellationToken = default);

    Task<Result<BoardMemberResponse>> AddMemberAsync(int boardId, AddBoardMemberRequest request, CancellationToken cancellationToken = default);
}

using Kanban.API.Common;
using Kanban.API.DTOs.Boards.Columns;

namespace Kanban.API.Services;

public interface IColumnService
{
    Task<Result<ColumnResponse>> UpdateAsync(int boardId, int columnId, UpdateColumnRequest request, CancellationToken cancellationToken = default);
    Task<Result<ColumnResponse>> CreateAsync(int boardId, CreateColumnRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int boardId, int columnId, CancellationToken cancellationToken = default);
}

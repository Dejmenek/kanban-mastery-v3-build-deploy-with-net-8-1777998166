using Kanban.API.Common;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.Services;

public interface IRetryExecutor
{
    Task<Result<T>> ExecuteAsync<T>(
        int maxAttempts,
        Func<Task<Result<T>>> operation,
        Func<DbUpdateException, bool> isRetryable,
        Func<Result<T>> onExhausted,
        CancellationToken cancellationToken);
}

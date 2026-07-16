using Kanban.API.Common;
using Kanban.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.Services;

public class DbUpdateRetryExecutor(ApplicationDbContext context) : IRetryExecutor
{
    public async Task<Result<T>> ExecuteAsync<T>(
        int maxAttempts, Func<Task<Result<T>>> operation, Func<DbUpdateException, bool> isRetryable,
        Func<Result<T>> onExhausted, CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (true)
        {
            attempt++;

            try
            {
                return await operation();
            }
            catch (DbUpdateException ex) when (isRetryable(ex))
            {
                context.ChangeTracker.Clear();
                if (attempt >= maxAttempts)
                {
                    return onExhausted();
                }
            }
        }
    }
}

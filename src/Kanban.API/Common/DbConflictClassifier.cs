using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.Common;

public static class DbConflictClassifier
{
    public static bool IsUniqueConstraintViolation(DbUpdateException ex, params string[] scopeHints) =>
        TryGetErrorInfo(ex, out var provider, out var primaryCode, out var extendedCode, out var message) &&
        DbErrorClassifier.IsUniqueConstraintViolation(provider, primaryCode, extendedCode, message, scopeHints);

    public static bool IsForeignKeyViolation(DbUpdateException ex) =>
        TryGetErrorInfo(ex, out var provider, out var primaryCode, out var extendedCode, out _) &&
        DbErrorClassifier.IsForeignKeyViolation(provider, primaryCode, extendedCode);

    public static bool IsTransient(DbUpdateException ex) =>
        TryGetErrorInfo(ex, out var provider, out var primaryCode, out _, out _) &&
        DbErrorClassifier.IsTransient(provider, primaryCode);

    public static bool IsRetryableConflict(DbUpdateException ex, params string[] scopeHints) =>
        IsUniqueConstraintViolation(ex, scopeHints) || IsTransient(ex);

    private static bool TryGetErrorInfo(
        DbUpdateException ex, out DbProvider provider, out int primaryCode, out int extendedCode, out string message)
    {
        switch (ex.InnerException)
        {
            case SqliteException sqliteEx:
                provider = DbProvider.Sqlite;
                primaryCode = sqliteEx.SqliteErrorCode;
                extendedCode = sqliteEx.SqliteExtendedErrorCode;
                message = sqliteEx.Message;
                return true;
            case SqlException sqlEx:
                provider = DbProvider.SqlServer;
                primaryCode = sqlEx.Number;
                extendedCode = sqlEx.Number;
                message = sqlEx.Message;
                return true;
            default:
                provider = DbProvider.Unknown;
                primaryCode = 0;
                extendedCode = 0;
                message = string.Empty;
                return false;
        }
    }
}

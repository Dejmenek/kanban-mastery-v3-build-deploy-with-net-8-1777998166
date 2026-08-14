namespace Kanban.API.Common;

public static class DbErrorClassifier
{
    private const int SqliteConstraintUnique = 2067;
    private const int SqliteConstraintForeignKey = 787;
    private const int SqliteConstraintTrigger = 1811;
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;

    private const int SqlServerUniqueIndexViolation = 2601;
    private const int SqlServerUniqueConstraintViolation = 2627;
    private const int SqlServerConstraintConflict = 547;
    private const int SqlServerDeadlockVictim = 1205;
    private const int SqlServerLockRequestTimeout = 1222;

    public static bool IsUniqueConstraintViolation(
        DbProvider provider, int primaryCode, int extendedCode, string message, IReadOnlyCollection<string>? scopeHints = null)
    {
        var isUniqueViolation = provider switch
        {
            DbProvider.Sqlite => extendedCode == SqliteConstraintUnique,
            DbProvider.SqlServer => primaryCode is SqlServerUniqueIndexViolation or SqlServerUniqueConstraintViolation,
            _ => false
        };

        if (!isUniqueViolation) return false;
        if (scopeHints is null || scopeHints.Count == 0) return true;

        return scopeHints.Any(hint => message.Contains(hint, StringComparison.Ordinal));
    }

    public static bool IsForeignKeyViolation(DbProvider provider, int primaryCode, int extendedCode) =>
        provider switch
        {
            DbProvider.Sqlite => extendedCode is SqliteConstraintForeignKey or SqliteConstraintTrigger,
            DbProvider.SqlServer => primaryCode == SqlServerConstraintConflict,
            _ => false
        };

    public static bool IsTransient(DbProvider provider, int primaryCode) =>
        provider switch
        {
            DbProvider.Sqlite => primaryCode is SqliteBusy or SqliteLocked,
            DbProvider.SqlServer => primaryCode is SqlServerDeadlockVictim or SqlServerLockRequestTimeout,
            _ => false
        };
}

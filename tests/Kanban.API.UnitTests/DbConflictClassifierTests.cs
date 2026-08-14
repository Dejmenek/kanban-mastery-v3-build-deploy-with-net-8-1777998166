using Kanban.API.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.UnitTests;

public class DbConflictClassifierTests
{
    private static DbUpdateException WrapSqlite(string message, int errorCode, int extendedErrorCode) =>
        new("update failed", new SqliteException(message, errorCode, extendedErrorCode));

    [Fact]
    public void IsUniqueConstraintViolation_WithSqliteUniqueException_ScopeMatches_ReturnsTrue()
    {
        // Arrange
        var ex = WrapSqlite("UNIQUE constraint failed: Cards.ColumnId, Cards.Position", errorCode: 19, extendedErrorCode: 2067);

        // Act
        var result = DbConflictClassifier.IsUniqueConstraintViolation(ex, "Cards.ColumnId, Cards.Position");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithSqliteUniqueException_ScopeDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        var ex = WrapSqlite("UNIQUE constraint failed: Users.Email", errorCode: 19, extendedErrorCode: 2067);

        // Act
        var result = DbConflictClassifier.IsUniqueConstraintViolation(ex, "Cards.ColumnId, Cards.Position");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(787)]
    [InlineData(1811)]
    public void IsForeignKeyViolation_WithSqliteForeignKeyException_ReturnsTrue(int extendedErrorCode)
    {
        // Arrange
        var ex = WrapSqlite("FOREIGN KEY constraint failed", errorCode: 19, extendedErrorCode);

        // Act
        var result = DbConflictClassifier.IsForeignKeyViolation(ex);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsForeignKeyViolation_WithSqliteUniqueException_ReturnsFalse()
    {
        // Arrange
        var ex = WrapSqlite("UNIQUE constraint failed: Cards.ColumnId, Cards.Position", errorCode: 19, extendedErrorCode: 2067);

        // Act
        var result = DbConflictClassifier.IsForeignKeyViolation(ex);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTransient_WithSqliteBusyException_ReturnsTrue()
    {
        // Arrange
        var ex = WrapSqlite("database is locked", errorCode: 5, extendedErrorCode: 5);

        // Act
        var result = DbConflictClassifier.IsTransient(ex);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTransient_WithSqliteLockedException_ReturnsTrue()
    {
        // Arrange
        var ex = WrapSqlite("database table is locked", errorCode: 6, extendedErrorCode: 6);

        // Act
        var result = DbConflictClassifier.IsTransient(ex);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRetryableConflict_WithScopedUniqueViolation_ReturnsTrue()
    {
        // Arrange
        var ex = WrapSqlite("UNIQUE constraint failed: Cards.ColumnId, Cards.Position", errorCode: 19, extendedErrorCode: 2067);

        // Act
        var result = DbConflictClassifier.IsRetryableConflict(ex, "Cards.ColumnId, Cards.Position");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRetryableConflict_WithTransientLockError_ReturnsTrue()
    {
        // Arrange
        var ex = WrapSqlite("database is locked", errorCode: 5, extendedErrorCode: 5);

        // Act
        var result = DbConflictClassifier.IsRetryableConflict(ex, "Cards.ColumnId, Cards.Position");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRetryableConflict_WithUnscopedUniqueViolation_ReturnsFalse()
    {
        // Arrange
        var ex = WrapSqlite("UNIQUE constraint failed: Users.Email", errorCode: 19, extendedErrorCode: 2067);

        // Act
        var result = DbConflictClassifier.IsRetryableConflict(ex, "Cards.ColumnId, Cards.Position");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithNonProviderInnerException_ReturnsFalse()
    {
        // Arrange
        var ex = new DbUpdateException("update failed", new InvalidOperationException("boom"));

        // Act
        var result = DbConflictClassifier.IsUniqueConstraintViolation(ex);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithNoInnerException_ReturnsFalse()
    {
        // Arrange
        var ex = new DbUpdateException("update failed");

        // Act
        var result = DbConflictClassifier.IsUniqueConstraintViolation(ex);

        // Assert
        Assert.False(result);
    }
}

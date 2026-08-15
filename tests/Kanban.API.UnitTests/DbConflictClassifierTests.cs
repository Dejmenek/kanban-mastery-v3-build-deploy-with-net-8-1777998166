using Kanban.API.Common;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.UnitTests;

// SqlException has no public constructor, so provider-specific exception unwrapping can't be unit-tested
// with a hand-built exception here. That path (real SqlException -> IsRetryableConflict) is exercised
// end-to-end by the SQL Server-backed integration tests instead (see CardServiceTests/ColumnServiceTests).
public class DbConflictClassifierTests
{
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

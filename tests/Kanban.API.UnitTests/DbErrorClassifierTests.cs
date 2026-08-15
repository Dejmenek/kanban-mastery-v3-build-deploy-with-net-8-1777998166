using Kanban.API.Common;

namespace Kanban.API.UnitTests;

public class DbErrorClassifierTests
{
    [Theory]
    [InlineData(2627)]
    [InlineData(2601)]
    public void IsUniqueConstraintViolation_SqlServer_WithUniqueViolationCode_ReturnsTrue(int primaryCode)
    {
        // Act
        var result = DbErrorClassifier.IsUniqueConstraintViolation(DbProvider.SqlServer, primaryCode, extendedCode: primaryCode, message: "");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_SqlServer_WithUnrelatedCode_ReturnsFalse()
    {
        // Act
        var result = DbErrorClassifier.IsUniqueConstraintViolation(DbProvider.SqlServer, primaryCode: 547, extendedCode: 547, message: "");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithMatchingScopeHint_ReturnsTrue()
    {
        // Act
        var result = DbErrorClassifier.IsUniqueConstraintViolation(
            DbProvider.SqlServer, primaryCode: 2627, extendedCode: 2627,
            message: "Violation of UNIQUE KEY constraint. Cannot insert duplicate key in object 'dbo.Cards'. The duplicate key value is (Cards.ColumnId, Cards.Position).",
            scopeHints: ["Cards.ColumnId, Cards.Position"]);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithNonMatchingScopeHint_ReturnsFalse()
    {
        // Act
        var result = DbErrorClassifier.IsUniqueConstraintViolation(
            DbProvider.SqlServer, primaryCode: 2627, extendedCode: 2627,
            message: "Violation of UNIQUE KEY constraint. Cannot insert duplicate key in object 'dbo.Users'. The duplicate key value is (Users.Email).",
            scopeHints: ["Cards.ColumnId, Cards.Position"]);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithNoScopeHints_ReturnsTrueForAnyUniqueViolation()
    {
        // Act
        var result = DbErrorClassifier.IsUniqueConstraintViolation(
            DbProvider.SqlServer, primaryCode: 2627, extendedCode: 2627, message: "unrelated message");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_UnknownProvider_ReturnsFalse()
    {
        // Act
        var result = DbErrorClassifier.IsUniqueConstraintViolation(DbProvider.Unknown, primaryCode: 2627, extendedCode: 2627, message: "");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsForeignKeyViolation_SqlServer_WithCode547_ReturnsTrue()
    {
        // Act
        var result = DbErrorClassifier.IsForeignKeyViolation(DbProvider.SqlServer, primaryCode: 547, extendedCode: 547);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsForeignKeyViolation_SqlServer_WithUnrelatedCode_ReturnsFalse()
    {
        // Act
        var result = DbErrorClassifier.IsForeignKeyViolation(DbProvider.SqlServer, primaryCode: 2627, extendedCode: 2627);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(1205)]
    [InlineData(1222)]
    public void IsTransient_SqlServer_DeadlockOrLockTimeout_ReturnsTrue(int primaryCode)
    {
        // Act
        var result = DbErrorClassifier.IsTransient(DbProvider.SqlServer, primaryCode);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTransient_SqlServer_UnrelatedCode_ReturnsFalse()
    {
        // Act
        var result = DbErrorClassifier.IsTransient(DbProvider.SqlServer, primaryCode: 547);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTransient_UnknownProvider_ReturnsFalse()
    {
        // Act
        var result = DbErrorClassifier.IsTransient(DbProvider.Unknown, primaryCode: 5);

        // Assert
        Assert.False(result);
    }
}

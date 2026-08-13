using Kanban.API.Common;

namespace Kanban.API.UnitTests;

public class PositionResolverTests
{
    [Fact]
    public void Resolve_WithNullPosition_ReturnsExistingCountPlusOne()
    {
        // Act
        var result = PositionResolver.Resolve(null, 5);

        // Assert
        Assert.Equal(6, result);
    }

    [Theory]
    [InlineData(1, 5, 1)]
    [InlineData(3, 5, 3)]
    [InlineData(5, 5, 5)]
    [InlineData(6, 5, 6)]
    public void Resolve_WithPositionWithinValidRange_ReturnsRequestedPosition(int requestedPosition, int existingCount, int expected)
    {
        // Act
        var result = PositionResolver.Resolve(requestedPosition, existingCount);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Resolve_WithPositionBelowOne_FallsBackToExistingCountPlusOne(int requestedPosition)
    {
        // Arrange
        const int existingCount = 5;

        // Act
        var result = PositionResolver.Resolve(requestedPosition, existingCount);

        // Assert
        Assert.Equal(existingCount + 1, result);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(100)]
    public void Resolve_WithPositionAboveExistingCountPlusOne_FallsBackToExistingCountPlusOne(int requestedPosition)
    {
        // Arrange
        const int existingCount = 5;

        // Act
        var result = PositionResolver.Resolve(requestedPosition, existingCount);

        // Assert
        Assert.Equal(existingCount + 1, result);
    }

    [Fact]
    public void Resolve_OnEmptyCollection_WithNullPosition_ReturnsOne()
    {
        // Act
        var result = PositionResolver.Resolve(null, 0);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void Resolve_OnEmptyCollection_WithPositionOne_ReturnsOne()
    {
        // Act
        var result = PositionResolver.Resolve(1, 0);

        // Assert
        Assert.Equal(1, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Resolve_OnEmptyCollection_WithOutOfRangePosition_ReturnsOne(int requestedPosition)
    {
        // Act
        var result = PositionResolver.Resolve(requestedPosition, 0);

        // Assert
        Assert.Equal(1, result);
    }
}

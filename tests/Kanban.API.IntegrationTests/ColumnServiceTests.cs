using Kanban.API.Common;
using Kanban.API.DTOs.Boards.Columns;
using Kanban.API.Errors;
using Kanban.API.Models;
using Kanban.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.IntegrationTests;

public class ColumnServiceTests(IntegrationTestWebAppFactory<Program> factory)
    : IntegrationTestBase(factory), IClassFixture<IntegrationTestWebAppFactory<Program>>
{
    private sealed class AlwaysExhaustedRetryExecutor : IRetryExecutor
    {
        public Task<Result<T>> ExecuteAsync<T>(
            int maxAttempts, Func<Task<Result<T>>> operation, Func<DbUpdateException, bool> isRetryable,
            Func<Result<T>> onExhausted, CancellationToken cancellationToken)
            => Task.FromResult(onExhausted());
    }

    [Fact]
    public async Task CreateAsync_OnEmptyBoard_CreatesColumnAtPositionOne()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateColumnRequest("To Do", null, null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("To Do", result.Value.Title);
        Assert.Equal(1, result.Value.Position);
        Assert.Empty(result.Value.Cards);

        var columnCount = await UseDbContextAsync(context => context.Columns.CountAsync(c => c.BoardId == board.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, columnCount);
    }

    [Fact]
    public async Task CreateAsync_WithPositionOmitted_AppendsAtCountPlusOne()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        await UseDbContextAsync(async context =>
        {
            await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Col 1", Position = 1 });
            await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Col 2", Position = 2 });
        });

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateColumnRequest("New", null, null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Position);

        var positions = await UseDbContextAsync(context => context.Columns
            .Where(c => c.BoardId == board.Id)
            .OrderBy(c => c.Position)
            .Select(c => c.Position)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal([1, 2, 3], positions);
    }

    [Fact]
    public async Task CreateAsync_WithPositionAtCountPlusOne_AppendsWithoutShift()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var (first, second) = await UseDbContextAsync(async context =>
        {
            var c1 = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Col 1", Position = 1 });
            var c2 = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Col 2", Position = 2 });
            return (c1, c2);
        });

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateColumnRequest("New", null, 3), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Position);

        var firstPosition = await UseDbContextAsync(context => context.Columns.Where(c => c.Id == first.Id).Select(c => c.Position).SingleAsync(TestContext.Current.CancellationToken));
        var secondPosition = await UseDbContextAsync(context => context.Columns.Where(c => c.Id == second.Id).Select(c => c.Position).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, firstPosition);
        Assert.Equal(2, secondPosition);
    }

    [Fact]
    public async Task CreateAsync_WithValidMiddlePosition_ShiftsLaterColumns()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var (first, second, third) = await UseDbContextAsync(async context =>
        {
            var c1 = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Col 1", Position = 1 });
            var c2 = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Col 2", Position = 2 });
            var c3 = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Col 3", Position = 3 });
            return (c1, c2, c3);
        });

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateColumnRequest("Inserted", null, 2), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Position);

        var positionsById = await UseDbContextAsync(context => context.Columns
            .Where(c => c.BoardId == board.Id)
            .ToDictionaryAsync(c => c.Id, c => c.Position, TestContext.Current.CancellationToken));

        Assert.Equal(1, positionsById[first.Id]);
        Assert.Equal(3, positionsById[second.Id]);
        Assert.Equal(4, positionsById[third.Id]);
        Assert.Equal(2, positionsById[result.Value.Id]);
    }

    [Fact]
    public async Task CreateAsync_WithPositionOne_ShiftsAllExistingColumns()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var (first, second) = await UseDbContextAsync(async context =>
        {
            var c1 = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Col 1", Position = 1 });
            var c2 = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Col 2", Position = 2 });
            return (c1, c2);
        });

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateColumnRequest("First", null, 1), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Position);

        var positionsById = await UseDbContextAsync(context => context.Columns
            .Where(c => c.BoardId == board.Id)
            .ToDictionaryAsync(c => c.Id, c => c.Position, TestContext.Current.CancellationToken));

        Assert.Equal(2, positionsById[first.Id]);
        Assert.Equal(3, positionsById[second.Id]);
        Assert.Equal(1, positionsById[result.Value.Id]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100)]
    public async Task CreateAsync_WithOutOfRangePosition_FallsBackToAppend(int requestedPosition)
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        await UseDbContextAsync(context => BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Col 1", Position = 1 }));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateColumnRequest("New", null, requestedPosition), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Position);

        var positions = await UseDbContextAsync(context => context.Columns
            .Where(c => c.BoardId == board.Id)
            .OrderBy(c => c.Position)
            .Select(c => c.Position)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal([1, 2], positions);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentBoard_ReturnsNotFoundAndPersistsNothing()
    {
        // Arrange
        const int nonExistentBoardId = 999;

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(nonExistentBoardId, new CreateColumnRequest("New", null, null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(BoardErrors.NotFound(nonExistentBoardId), result.Error);

        var columnCount = await UseDbContextAsync(context => context.Columns.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, columnCount);
    }

    [Fact]
    public async Task CreateAsync_WithTitleAndDescription_RoundTripsInResponse()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateColumnRequest("In Progress", "Work being actively done", null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("In Progress", result.Value.Title);
        Assert.Equal("Work being actively done", result.Value.Description);

        var column = await UseDbContextAsync(context => context.Columns.SingleAsync(c => c.Id == result.Value.Id, TestContext.Current.CancellationToken));
        Assert.Equal("In Progress", column.Title);
        Assert.Equal("Work being actively done", column.Description);
    }

    [Fact]
    public async Task CreateAsync_WithNullDescription_PersistsNullDescription()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateColumnRequest("Backlog", null, null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Description);

        var column = await UseDbContextAsync(context => context.Columns.SingleAsync(c => c.Id == result.Value.Id, TestContext.Current.CancellationToken));
        Assert.Null(column.Description);
    }

    [Fact]
    public async Task CreateAsync_ReturnedIdMatchesPersistedRow()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateColumnRequest("To Do", null, null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Id > 0);

        var column = await UseDbContextAsync(async context => await context.Columns.FindAsync([result.Value.Id], TestContext.Current.CancellationToken));
        Assert.NotNull(column);
        Assert.Equal(result.Value.Title, column.Title);
        Assert.Equal(result.Value.Position, column.Position);
        Assert.Equal(board.Id, column.BoardId);
    }

    [Fact]
    public async Task CreateAsync_MultipleSequentialCreates_MaintainDensePositionOrdering()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        for (var i = 0; i < 3; i++)
        {
            var result = await UseColumnServiceAsync(service =>
                service.CreateAsync(board.Id, new CreateColumnRequest($"Col {i}", null, null), TestContext.Current.CancellationToken));
            Assert.True(result.IsSuccess);
        }

        // Assert
        var positions = await UseDbContextAsync(context => context.Columns
            .Where(c => c.BoardId == board.Id)
            .OrderBy(c => c.Position)
            .Select(c => c.Position)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal([1, 2, 3], positions);
    }

    [Fact]
    public async Task CreateAsync_NewColumnResponse_HasEmptyCardsList()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateColumnRequest("To Do", null, null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Cards);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithInvalidTitle_ReturnsValidationFailureAndPersistsNothing(string? title)
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateColumnRequest(title!, null, null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ColumnErrors.InvalidTitle, result.Error);

        var columnCount = await UseDbContextAsync(context => context.Columns.CountAsync(c => c.BoardId == board.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, columnCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_WithInvalidTitle_ReturnsValidationFailureAndPersistsNothing(string? title)
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Original", Position = 1 }));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.UpdateAsync(board.Id, column.Id, new UpdateColumnRequest(title!, null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ColumnErrors.InvalidTitle, result.Error);

        var persisted = await UseDbContextAsync(context => context.Columns.SingleAsync(c => c.Id == column.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Original", persisted.Title);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentColumn_ReturnsNotFoundFailure()
    {
        // Arrange
        const int nonExistentColumnId = 999;
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.UpdateAsync(board.Id, nonExistentColumnId, new UpdateColumnRequest("New Title", null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ColumnErrors.NotFound(nonExistentColumnId), result.Error);
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesColumnAndReturnsSuccess()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Original", Position = 1 }));

        var request = new UpdateColumnRequest("Updated Title", "Updated description");

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.UpdateAsync(board.Id, column.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(request.Title, result.Value.Title);
        Assert.Equal(request.Description, result.Value.Description);
        Assert.Equal(1, result.Value.Position);
        Assert.Empty(result.Value.Cards);

        var persisted = await UseDbContextAsync(context => context.Columns.SingleAsync(c => c.Id == column.Id, TestContext.Current.CancellationToken));
        Assert.Equal(request.Title, persisted.Title);
        Assert.Equal(request.Description, persisted.Description);
        Assert.Equal(1, persisted.Position);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentColumn_ReturnsNotFoundFailure()
    {
        // Arrange
        const int nonExistentColumnId = 999;
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.DeleteAsync(board.Id, nonExistentColumnId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ColumnErrors.NotFound(nonExistentColumnId), result.Error);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingColumn_DeletesColumnAndPersists()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Original", Position = 1 }));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.DeleteAsync(board.Id, column.Id, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);

        var columnCount = await UseDbContextAsync(context => context.Columns.CountAsync(c => c.BoardId == board.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, columnCount);
    }

    [Fact]
    public async Task DeleteAsync_WithCards_ReturnsHasCardsFailure()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Original", Position = 1 }));
        await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "A card", Position = 1 }));

        // Act
        var result = await UseColumnServiceAsync(service =>
            service.DeleteAsync(board.Id, column.Id, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ColumnErrors.HasCards(column.Id), result.Error);

        var columnStillExists = await UseDbContextAsync(context => context.Columns.AnyAsync(c => c.Id == column.Id, TestContext.Current.CancellationToken));
        Assert.True(columnStillExists);
    }

    [Fact]
    public async Task CreateAsync_WhenRetriesExhausted_ReturnsPositionConflictFailureAndPersistsNothing()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseDbContextAsync(context =>
        {
            var service = new ColumnService(context, new AlwaysExhaustedRetryExecutor());
            return service.CreateAsync(board.Id, new CreateColumnRequest("New", null, null), TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ColumnErrors.PositionConflict(board.Id), result.Error);

        var columnCount = await UseDbContextAsync(context => context.Columns.CountAsync(c => c.BoardId == board.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, columnCount);
    }
}

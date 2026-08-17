using Kanban.API.Common;
using Kanban.API.DTOs.Boards.Cards;
using Kanban.API.Errors;
using Kanban.API.Models;
using Kanban.API.Notifiers;
using Kanban.API.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Kanban.API.IntegrationTests;

public class CardServiceTests(IntegrationTestWebAppFactory<Program> factory)
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
    public async Task CreateAsync_OnEmptyColumn_CreatesCardAtPositionOne()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateCardRequest("First card", null, column.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Position);

        var card = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == result.Value.Id, TestContext.Current.CancellationToken));
        Assert.Equal("First card", card.Title);
        Assert.Equal(column.Id, card.ColumnId);
        Assert.Equal(1, card.Position);
    }

    [Fact]
    public async Task CreateAsync_WithExistingCards_AppendsAtCountPlusOne()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        await UseDbContextAsync(async context =>
        {
            await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 1", Position = 1 });
            await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 2", Position = 2 });
        });

        // Act
        var result = await UseCardServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateCardRequest("Card 3", null, column.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Position);

        var positions = await UseDbContextAsync(context => context.Cards
            .Where(c => c.ColumnId == column.Id)
            .OrderBy(c => c.Position)
            .Select(c => c.Position)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal([1, 2, 3], positions);
    }

    [Fact]
    public async Task CreateAsync_AfterDeletingMiddleCard_FillsGapInsteadOfCollidingWithLastCard()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var (card1, card2, card3) = await UseDbContextAsync(async context =>
        {
            var c1 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 1", Position = 1 });
            var c2 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 2", Position = 2 });
            var c3 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 3", Position = 3 });
            return (c1, c2, c3);
        });

        var deleteResult = await UseCardServiceAsync(service =>
            service.DeleteAsync(board.Id, card2.Id, TestContext.Current.CancellationToken));
        Assert.True(deleteResult.IsSuccess);

        // Act
        var createResult = await UseCardServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateCardRequest("Card 4", null, column.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(createResult.IsSuccess);
        Assert.Equal(3, createResult.Value.Position);

        var cardsByPosition = await UseDbContextAsync(context => context.Cards
            .Where(c => c.ColumnId == column.Id)
            .OrderBy(c => c.Position)
            .Select(c => new { c.Id, c.Position })
            .ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal([1, 2, 3], cardsByPosition.Select(c => c.Position));
        Assert.Equal([card1.Id, card3.Id, createResult.Value.Id], cardsByPosition.Select(c => c.Id));
    }

    [Fact]
    public async Task CreateAsync_WithTitleAndDescription_RoundTripsAndPersists()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateCardRequest("Design review", "Review the new mockups", column.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Design review", result.Value.Title);
        Assert.Equal("Review the new mockups", result.Value.Description);

        var card = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == result.Value.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Design review", card.Title);
        Assert.Equal("Review the new mockups", card.Description);
    }

    [Fact]
    public async Task CreateAsync_WithNullDescription_PersistsNull()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateCardRequest("No description", null, column.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Description);

        var card = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == result.Value.Id, TestContext.Current.CancellationToken));
        Assert.Null(card.Description);
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
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateCardRequest(title!, null, column.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.InvalidTitle, result.Error);

        var cardCount = await UseDbContextAsync(context => context.Cards.CountAsync(c => c.ColumnId == column.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, cardCount);
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentColumn_ReturnsColumnNotFoundAndPersistsNothing()
    {
        // Arrange
        const int nonExistentColumnId = 999;
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.CreateAsync(board.Id, new CreateCardRequest("New card", null, nonExistentColumnId), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ColumnErrors.NotFound(nonExistentColumnId), result.Error);

        var cardCount = await UseDbContextAsync(context => context.Cards.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, cardCount);
    }

    [Fact]
    public async Task CreateAsync_WithColumnOnAnotherBoard_ReturnsColumnNotFound()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var boardA = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var boardB = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id, new Board { Name = "Board B" }));
        var columnOnBoardB = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = boardB.Id, Title = "Other board's column", Position = 1 }));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.CreateAsync(boardA.Id, new CreateCardRequest("New card", null, columnOnBoardB.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ColumnErrors.NotFound(columnOnBoardB.Id), result.Error);

        var cardCount = await UseDbContextAsync(context => context.Cards.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, cardCount);
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesTitleAndDescriptionAndPersists()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Original", Position = 1 }));

        var request = new UpdateCardRequest("Updated title", "Updated description");

        // Act
        var result = await UseCardServiceAsync(service =>
            service.UpdateAsync(board.Id, card.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(request.Title, result.Value.Title);
        Assert.Equal(request.Description, result.Value.Description);
        Assert.Equal(1, result.Value.Position);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal(request.Title, persisted.Title);
        Assert.Equal(request.Description, persisted.Description);
        Assert.Equal(1, persisted.Position);
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
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Original", Position = 1 }));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.UpdateAsync(board.Id, card.Id, new UpdateCardRequest(title!, null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.InvalidTitle, result.Error);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Original", persisted.Title);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentCard_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentCardId = 999;
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.UpdateAsync(board.Id, nonExistentCardId, new UpdateCardRequest("New Title", null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.NotFound(nonExistentCardId), result.Error);
    }

    [Fact]
    public async Task MoveAsync_ToAnotherColumn_UpdatesColumnIdAndPersists()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var (columnA, columnB) = await UseDbContextAsync(async context =>
        {
            var colA = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 });
            var colB = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Done", Position = 2 });
            return (colA, colB);
        });
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = columnA.Id, Title = "Move me", Position = 1 }));

        var request = new MoveCardRequest(columnB.Id, 1, columnA.Id, 1);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, card.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Position);
        Assert.Equal(columnB.Id, result.Value.ColumnId);
        Assert.Equal(2, result.Value.AffectedColumns.Count);
        var sourceAffected = result.Value.AffectedColumns.Single(c => c.ColumnId == columnA.Id);
        Assert.Empty(sourceAffected.Cards);
        var destinationAffected = result.Value.AffectedColumns.Single(c => c.ColumnId == columnB.Id);
        Assert.Equal(card.Id, Assert.Single(destinationAffected.Cards).CardId);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal(columnB.Id, persisted.ColumnId);
        Assert.Equal(1, persisted.Position);
    }

    [Fact]
    public async Task MoveAsync_ToNonExistentColumn_ReturnsColumnNotFoundAndPersistsNothing()
    {
        // Arrange
        const int nonExistentColumnId = 999;
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Stay put", Position = 1 }));

        var request = new MoveCardRequest(nonExistentColumnId, 1, column.Id, 1);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, card.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ColumnErrors.NotFound(nonExistentColumnId), result.Error);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal(column.Id, persisted.ColumnId);
    }

    [Fact]
    public async Task MoveAsync_CardBelongingToAnotherBoard_ReturnsNotFound()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var boardA = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var boardB = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id, new Board { Name = "Board B" }));
        var columnOnBoardB = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = boardB.Id, Title = "Col B", Position = 1 }));
        var cardOnBoardB = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = columnOnBoardB.Id, Title = "Card on board B", Position = 1 }));

        var request = new MoveCardRequest(columnOnBoardB.Id, 1, columnOnBoardB.Id, 1);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.MoveAsync(boardA.Id, cardOnBoardB.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.NotFound(cardOnBoardB.Id), result.Error);
    }

    [Fact]
    public async Task MoveAsync_SameColumnReorderForward_ShiftsSiblingsBetweenOldAndNewPosition()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var (card1, card2, card3, card4, card5) = await UseDbContextAsync(async context =>
        {
            var c1 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 1", Position = 1 });
            var c2 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 2", Position = 2 });
            var c3 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 3", Position = 3 });
            var c4 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 4", Position = 4 });
            var c5 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 5", Position = 5 });
            return (c1, c2, c3, c4, c5);
        });

        var request = new MoveCardRequest(column.Id, 3, column.Id, 1);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, card1.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Position);

        var positionsById = await UseDbContextAsync(context => context.Cards
            .Where(c => c.ColumnId == column.Id)
            .ToDictionaryAsync(c => c.Id, c => c.Position, TestContext.Current.CancellationToken));

        Assert.Equal(3, positionsById[card1.Id]);
        Assert.Equal(1, positionsById[card2.Id]);
        Assert.Equal(2, positionsById[card3.Id]);
        Assert.Equal(4, positionsById[card4.Id]);
        Assert.Equal(5, positionsById[card5.Id]);

        var affected = Assert.Single(result.Value.AffectedColumns);
        Assert.Equal(column.Id, affected.ColumnId);
        var affectedPositionsById = affected.Cards.ToDictionary(c => c.CardId, c => c.Position);
        Assert.Equal(positionsById, affectedPositionsById);
    }

    [Fact]
    public async Task MoveAsync_SameColumnReorderBackward_ShiftsSiblingsBetweenNewAndOldPosition()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var (card1, card2, card3, card4, card5) = await UseDbContextAsync(async context =>
        {
            var c1 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 1", Position = 1 });
            var c2 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 2", Position = 2 });
            var c3 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 3", Position = 3 });
            var c4 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 4", Position = 4 });
            var c5 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 5", Position = 5 });
            return (c1, c2, c3, c4, c5);
        });

        var request = new MoveCardRequest(column.Id, 1, column.Id, 4);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, card4.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Position);

        var positionsById = await UseDbContextAsync(context => context.Cards
            .Where(c => c.ColumnId == column.Id)
            .ToDictionaryAsync(c => c.Id, c => c.Position, TestContext.Current.CancellationToken));

        Assert.Equal(1, positionsById[card4.Id]);
        Assert.Equal(2, positionsById[card1.Id]);
        Assert.Equal(3, positionsById[card2.Id]);
        Assert.Equal(4, positionsById[card3.Id]);
        Assert.Equal(5, positionsById[card5.Id]);
    }

    [Fact]
    public async Task MoveAsync_SameColumnWithUnchangedPositionValue_IsNoOpForSiblings()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var (card1, card2, card3) = await UseDbContextAsync(async context =>
        {
            var c1 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 1", Position = 1 });
            var c2 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 2", Position = 2 });
            var c3 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 3", Position = 3 });
            return (c1, c2, c3);
        });

        var request = new MoveCardRequest(column.Id, 2, column.Id, 2);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, card2.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Position);

        var positionsById = await UseDbContextAsync(context => context.Cards
            .Where(c => c.ColumnId == column.Id)
            .ToDictionaryAsync(c => c.Id, c => c.Position, TestContext.Current.CancellationToken));

        Assert.Equal(1, positionsById[card1.Id]);
        Assert.Equal(2, positionsById[card2.Id]);
        Assert.Equal(3, positionsById[card3.Id]);
    }

    [Fact]
    public async Task MoveAsync_CrossColumnMoveWithExplicitPosition_ClosesSourceGapAndOpensDestinationSlot()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var (source, destination) = await UseDbContextAsync(async context =>
        {
            var src = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 });
            var dst = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Done", Position = 2 });
            return (src, dst);
        });
        var (sourceCard1, movingCard, sourceCard3) = await UseDbContextAsync(async context =>
        {
            var c1 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = source.Id, Title = "Source 1", Position = 1 });
            var moving = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = source.Id, Title = "Moving", Position = 2 });
            var c3 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = source.Id, Title = "Source 3", Position = 3 });
            return (c1, moving, c3);
        });
        var (destCard1, destCard2) = await UseDbContextAsync(async context =>
        {
            var d1 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = destination.Id, Title = "Dest 1", Position = 1 });
            var d2 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = destination.Id, Title = "Dest 2", Position = 2 });
            return (d1, d2);
        });

        var request = new MoveCardRequest(destination.Id, 1, source.Id, 2);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, movingCard.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Position);

        var sourcePositionsById = await UseDbContextAsync(context => context.Cards
            .Where(c => c.ColumnId == source.Id)
            .ToDictionaryAsync(c => c.Id, c => c.Position, TestContext.Current.CancellationToken));
        Assert.Equal(1, sourcePositionsById[sourceCard1.Id]);
        Assert.Equal(2, sourcePositionsById[sourceCard3.Id]);

        var destinationPositionsById = await UseDbContextAsync(context => context.Cards
            .Where(c => c.ColumnId == destination.Id)
            .ToDictionaryAsync(c => c.Id, c => c.Position, TestContext.Current.CancellationToken));
        Assert.Equal(1, destinationPositionsById[movingCard.Id]);
        Assert.Equal(2, destinationPositionsById[destCard1.Id]);
        Assert.Equal(3, destinationPositionsById[destCard2.Id]);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == movingCard.Id, TestContext.Current.CancellationToken));
        Assert.Equal(destination.Id, persisted.ColumnId);

        Assert.Equal(2, result.Value.AffectedColumns.Count);
        var sourceAffected = result.Value.AffectedColumns.Single(c => c.ColumnId == source.Id);
        Assert.Equal(sourcePositionsById, sourceAffected.Cards.ToDictionary(c => c.CardId, c => c.Position));
        var destinationAffected = result.Value.AffectedColumns.Single(c => c.ColumnId == destination.Id);
        Assert.Equal(destinationPositionsById, destinationAffected.Cards.ToDictionary(c => c.CardId, c => c.Position));
    }

    [Fact]
    public async Task MoveAsync_CrossColumnMoveAfterPriorReorderChurnsRowOrder_DoesNotThrowAndProducesContiguousPositions()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var (source, destination) = await UseDbContextAsync(async context =>
        {
            var src = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 });
            var dst = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Done", Position = 2 });
            return (src, dst);
        });
        var (s1, s2, s3, s4) = await UseDbContextAsync(async context =>
        {
            var c1 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = source.Id, Title = "S1", Position = 1 });
            var c2 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = source.Id, Title = "S2", Position = 2 });
            var c3 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = source.Id, Title = "S3", Position = 3 });
            var c4 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = source.Id, Title = "S4", Position = 4 });
            return (c1, c2, c3, c4);
        });
        var (d1, d2) = await UseDbContextAsync(async context =>
        {
            var dd1 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = destination.Id, Title = "D1", Position = 1 });
            var dd2 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = destination.Id, Title = "D2", Position = 2 });
            return (dd1, dd2);
        });

        var churnResult = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, s4.Id, new MoveCardRequest(source.Id, 1, source.Id, 4), TestContext.Current.CancellationToken));
        Assert.True(churnResult.IsSuccess);

        // Act
        var moveResult = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, s1.Id, new MoveCardRequest(destination.Id, 1, source.Id, 2), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(moveResult.IsSuccess);
        Assert.Equal(1, moveResult.Value.Position);

        var sourcePositionsById = await UseDbContextAsync(context => context.Cards
            .Where(c => c.ColumnId == source.Id)
            .ToDictionaryAsync(c => c.Id, c => c.Position, TestContext.Current.CancellationToken));
        Assert.Equal(1, sourcePositionsById[s4.Id]);
        Assert.Equal(2, sourcePositionsById[s2.Id]);
        Assert.Equal(3, sourcePositionsById[s3.Id]);

        var destinationPositionsById = await UseDbContextAsync(context => context.Cards
            .Where(c => c.ColumnId == destination.Id)
            .ToDictionaryAsync(c => c.Id, c => c.Position, TestContext.Current.CancellationToken));
        Assert.Equal(1, destinationPositionsById[s1.Id]);
        Assert.Equal(2, destinationPositionsById[d1.Id]);
        Assert.Equal(3, destinationPositionsById[d2.Id]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100)]
    public async Task MoveAsync_WithOutOfRangePosition_FallsBackToAppend(int requestedPosition)
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var (card1, card2) = await UseDbContextAsync(async context =>
        {
            var c1 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 1", Position = 1 });
            var c2 = await BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 2", Position = 2 });
            return (c1, c2);
        });

        var request = new MoveCardRequest(column.Id, requestedPosition, column.Id, 1);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, card1.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Position);

        var positionsById = await UseDbContextAsync(context => context.Cards
            .Where(c => c.ColumnId == column.Id)
            .ToDictionaryAsync(c => c.Id, c => c.Position, TestContext.Current.CancellationToken));
        Assert.Equal(2, positionsById[card1.Id]);
        Assert.Equal(1, positionsById[card2.Id]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100)]
    public async Task MoveAsync_CrossColumnWithOutOfRangePosition_FallsBackToAppendAtDestinationEnd(int requestedPosition)
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var (source, destination) = await UseDbContextAsync(async context =>
        {
            var src = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 });
            var dst = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Done", Position = 2 });
            return (src, dst);
        });
        var movingCard = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = source.Id, Title = "Moving", Position = 1 }));
        await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = destination.Id, Title = "Dest 1", Position = 1 }));

        var request = new MoveCardRequest(destination.Id, requestedPosition, source.Id, 1);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, movingCard.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Position);
    }

    [Fact]
    public async Task MoveAsync_WithStaleExpectedPosition_ReturnsMoveConflictAndPersistsNothing()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Card 1", Position = 2 }));

        var request = new MoveCardRequest(column.Id, 1, column.Id, 1);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, card.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.MoveConflict(card.Id), result.Error);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal(2, persisted.Position);
        Assert.Equal(column.Id, persisted.ColumnId);
    }

    [Fact]
    public async Task MoveAsync_WithStaleExpectedColumnId_ReturnsMoveConflictAndPersistsNothing()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var (columnA, columnB) = await UseDbContextAsync(async context =>
        {
            var colA = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 });
            var colB = await BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Done", Position = 2 });
            return (colA, colB);
        });
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = columnA.Id, Title = "Card 1", Position = 1 }));

        var request = new MoveCardRequest(columnB.Id, 1, columnB.Id, 1);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.MoveAsync(board.Id, card.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.MoveConflict(card.Id), result.Error);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal(columnA.Id, persisted.ColumnId);
        Assert.Equal(1, persisted.Position);
    }

    [Fact]
    public async Task MoveAsync_WhenRetriesExhausted_ReturnsPositionConflictFailureAndPersistsNothing()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Original", Position = 1 }));

        var request = new MoveCardRequest(column.Id, 1, column.Id, 1);

        // Act
        var result = await UseDbContextAsync(context =>
        {
            var service = new CardService(context, new AlwaysExhaustedRetryExecutor(), Substitute.For<IBoardNotifier>());
            return service.MoveAsync(board.Id, card.Id, request, TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.PositionConflict(column.Id), result.Error);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Original", persisted.Title);
        Assert.Equal(1, persisted.Position);
        Assert.Equal(column.Id, persisted.ColumnId);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingCard_DeletesAndPersists()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Delete me", Position = 1 }));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.DeleteAsync(board.Id, card.Id, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);

        var cardCount = await UseDbContextAsync(context => context.Cards.CountAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, cardCount);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentCard_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentCardId = 999;
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.DeleteAsync(board.Id, nonExistentCardId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.NotFound(nonExistentCardId), result.Error);
    }

    [Fact]
    public async Task DeleteAsync_CardBelongingToAnotherBoard_ReturnsNotFoundAndDoesNotDelete()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var boardA = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var boardB = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id, new Board { Name = "Board B" }));
        var columnOnBoardB = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = boardB.Id, Title = "Col B", Position = 1 }));
        var cardOnBoardB = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = columnOnBoardB.Id, Title = "Card on board B", Position = 1 }));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.DeleteAsync(boardA.Id, cardOnBoardB.Id, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.NotFound(cardOnBoardB.Id), result.Error);

        var cardStillExists = await UseDbContextAsync(context => context.Cards.AnyAsync(c => c.Id == cardOnBoardB.Id, TestContext.Current.CancellationToken));
        Assert.True(cardStillExists);
    }

    [Fact]
    public async Task AssignCardToUserAsync_WithNonMemberUser_ReturnsUserNotMemberAndPersistsNothing()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Original", Position = 1 }));

        var nonMember = await CreateUserAsync("nonmember@example.com", "Test123!");

        // Act
        var result = await UseCardServiceAsync(service =>
            service.AssignCardToUserAsync(card.Id, board.Id, new AssignCardRequest(nonMember.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(BoardErrors.UserNotMember(nonMember.Id, board.Id), result.Error);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Null(persisted.AssignedToUserId);
    }

    [Fact]
    public async Task AssignCardToUserAsync_WithNonExistentCard_ReturnsNotFoundAndPersistsNothing()
    {
        // Arrange
        const int nonExistentCardId = 999;
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.AssignCardToUserAsync(nonExistentCardId, board.Id, new AssignCardRequest(owner.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.NotFound(nonExistentCardId), result.Error);

        var cardCount = await UseDbContextAsync(context => context.Cards.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, cardCount);
    }

    [Fact]
    public async Task AssignCardToUserAsync_WithValidRequest_AssignsAndPersists()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Original", Position = 1 }));

        var member = await CreateUserAsync("member@example.com", "Test123!");
        await UseDbContextAsync(context =>
            BoardTestHelper.SeedBoardMemberAsync(context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.AssignCardToUserAsync(card.Id, board.Id, new AssignCardRequest(member.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(card.Id, result.Value.Id);
        Assert.NotNull(result.Value.AssignedTo);
        Assert.Equal(member.Id, result.Value.AssignedTo.UserId);
        Assert.Equal(member.UserName, result.Value.AssignedTo.UserName);
        Assert.Equal(member.Email, result.Value.AssignedTo.Email);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal(member.Id, persisted.AssignedToUserId);
    }

    [Fact]
    public async Task UpdateAsync_WithAlreadyAssignedCard_PreservesAndReturnsAssigneeInfo()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));

        var member = await CreateUserAsync("member@example.com", "Test123!");
        await UseDbContextAsync(context =>
            BoardTestHelper.SeedBoardMemberAsync(context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Original", Position = 1, AssignedToUserId = member.Id }));

        var request = new UpdateCardRequest("Updated title", "Updated description");

        // Act
        var result = await UseCardServiceAsync(service =>
            service.UpdateAsync(board.Id, card.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.AssignedTo);
        Assert.Equal(member.Id, result.Value.AssignedTo.UserId);
        Assert.Equal(member.UserName, result.Value.AssignedTo.UserName);
        Assert.Equal(member.Email, result.Value.AssignedTo.Email);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal(member.Id, persisted.AssignedToUserId);
    }
}

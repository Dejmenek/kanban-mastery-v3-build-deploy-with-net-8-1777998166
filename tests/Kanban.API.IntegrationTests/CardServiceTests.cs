using Kanban.API.DTOs.Boards.Cards;
using Kanban.API.Errors;
using Kanban.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.IntegrationTests;

public class CardServiceTests(IntegrationTestWebAppFactory<Program> factory)
    : IntegrationTestBase(factory), IClassFixture<IntegrationTestWebAppFactory<Program>>
{
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

        var request = new UpdateCardRequest("Updated title", "Updated description", column.Id);

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
            service.UpdateAsync(board.Id, card.Id, new UpdateCardRequest(title!, null, column.Id), TestContext.Current.CancellationToken));

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
            service.UpdateAsync(board.Id, nonExistentCardId, new UpdateCardRequest("New Title", null, column.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.NotFound(nonExistentCardId), result.Error);
    }

    [Fact]
    public async Task UpdateAsync_MovingCardToAnotherColumn_UpdatesColumnIdAndPersists()
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

        var request = new UpdateCardRequest("Move me", null, columnB.Id);

        // Act
        var result = await UseCardServiceAsync(service =>
            service.UpdateAsync(board.Id, card.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal(columnB.Id, persisted.ColumnId);
    }

    [Fact]
    public async Task UpdateAsync_MovingToNonExistentColumn_ReturnsColumnNotFoundAndPersistsNothing()
    {
        // Arrange
        const int nonExistentColumnId = 999;
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Stay put", Position = 1 }));

        // Act
        var result = await UseCardServiceAsync(service =>
            service.UpdateAsync(board.Id, card.Id, new UpdateCardRequest("Stay put", null, nonExistentColumnId), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ColumnErrors.NotFound(nonExistentColumnId), result.Error);

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal(column.Id, persisted.ColumnId);
    }

    [Fact]
    public async Task UpdateAsync_CardBelongingToAnotherBoard_ReturnsNotFound()
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
            service.UpdateAsync(boardA.Id, cardOnBoardB.Id, new UpdateCardRequest("Hijacked", null, columnOnBoardB.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CardErrors.NotFound(cardOnBoardB.Id), result.Error);
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

        var persisted = await UseDbContextAsync(context => context.Cards.SingleAsync(c => c.Id == card.Id, TestContext.Current.CancellationToken));
        Assert.Equal(member.Id, persisted.AssignedToUserId);
    }
}

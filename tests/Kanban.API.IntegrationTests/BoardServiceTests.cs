using Kanban.API.DTOs.Boards;
using Kanban.API.Errors;
using Kanban.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.IntegrationTests;

public class BoardServiceTests(IntegrationTestWebAppFactory<Program> factory)
    : IntegrationTestBase(factory), IClassFixture<IntegrationTestWebAppFactory<Program>>
{
    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesBoardAndReturnsSuccess()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var request = new UpdateBoardRequest("Updated Name", "Updated description");

        // Act
        var result = await UseBoardServiceAsync(service =>
            service.UpdateAsync(board.Id, request, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(request.Name, result.Value.Name);
        Assert.Equal(request.Description, result.Value.Description);

        var persisted = await UseDbContextAsync(context => context.Boards.SingleAsync(b => b.Id == board.Id, TestContext.Current.CancellationToken));
        Assert.Equal(request.Name, persisted.Name);
        Assert.Equal(request.Description, persisted.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_WithInvalidName_ReturnsValidationFailureAndPersistsNothing(string? name)
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id, new Board { Name = "Original" }));

        // Act
        var result = await UseBoardServiceAsync(service =>
            service.UpdateAsync(board.Id, new UpdateBoardRequest(name!, null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(BoardErrors.InvalidName, result.Error);

        var persisted = await UseDbContextAsync(context => context.Boards.SingleAsync(b => b.Id == board.Id, TestContext.Current.CancellationToken));
        Assert.Equal("Original", persisted.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentBoard_ReturnsNotFoundFailure()
    {
        // Arrange
        const int nonExistentBoardId = 999;

        // Act
        var result = await UseBoardServiceAsync(service =>
            service.UpdateAsync(nonExistentBoardId, new UpdateBoardRequest("New Name", null), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(BoardErrors.NotFound(nonExistentBoardId), result.Error);
    }

    [Fact]
    public async Task DeleteAsync_RemovesBoardCardsColumnsAndMembershipsAndPersists()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "A card", Position = 1 }));

        var member = await CreateUserAsync("member@example.com", "Test123!");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        // Act
        var result = await UseBoardServiceAsync(service =>
            service.DeleteAsync(board.Id, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);

        var boardExists = await UseDbContextAsync(context => context.Boards.AnyAsync(b => b.Id == board.Id, TestContext.Current.CancellationToken));
        Assert.False(boardExists);

        var columnCount = await UseDbContextAsync(context => context.Columns.CountAsync(c => c.BoardId == board.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, columnCount);

        var cardCount = await UseDbContextAsync(context => context.Cards.CountAsync(c => c.ColumnId == column.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, cardCount);

        var membershipCount = await UseDbContextAsync(context => context.BoardsMemberships.CountAsync(m => m.BoardId == board.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, membershipCount);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentBoard_ReturnsSuccess()
    {
        // Arrange
        const int nonExistentBoardId = 999;

        // Act
        var result = await UseBoardServiceAsync(service =>
            service.DeleteAsync(nonExistentBoardId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
    }
}

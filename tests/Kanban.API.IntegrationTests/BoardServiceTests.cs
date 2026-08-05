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

    [Fact]
    public async Task GetByIdAsync_WithAssignedCard_ReturnsCardWithAssigneeInfo()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var member = await CreateUserAsync("member@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        await UseDbContextAsync(context => BoardTestHelper.SeedCardAsync(
            context, new Card { ColumnId = column.Id, Title = "A card", Position = 1, AssignedToUserId = member.Id }));

        // Act
        var result = await UseBoardServiceAsync(service =>
            service.GetByIdAsync(board.Id, owner.Id, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        var card = Assert.Single(result.Value.Columns.Single().Cards);
        Assert.NotNull(card.AssignedTo);
        Assert.Equal(member.Id, card.AssignedTo.UserId);
        Assert.Equal(member.UserName, card.AssignedTo.UserName);
        Assert.Equal(member.Email, card.AssignedTo.Email);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsBoardMembersWithEmailAndRole()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var member = await CreateUserAsync("member@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        // Act
        var result = await UseBoardServiceAsync(service =>
            service.GetByIdAsync(board.Id, owner.Id, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Members.Count);

        var returnedOwner = result.Value.Members.Single(m => m.MemberId == owner.Id);
        Assert.Equal(owner.UserName, returnedOwner.UserName);
        Assert.Equal(owner.Email, returnedOwner.Email);
        Assert.Equal(nameof(Role.Owner), returnedOwner.Role);

        var returnedMember = result.Value.Members.Single(m => m.MemberId == member.Id);
        Assert.Equal(member.UserName, returnedMember.UserName);
        Assert.Equal(member.Email, returnedMember.Email);
        Assert.Equal(nameof(Role.Member), returnedMember.Role);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUserRoleForRequestingUser()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var member = await CreateUserAsync("member@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        // Act
        var ownerResult = await UseBoardServiceAsync(service =>
            service.GetByIdAsync(board.Id, owner.Id, TestContext.Current.CancellationToken));
        var memberResult = await UseBoardServiceAsync(service =>
            service.GetByIdAsync(board.Id, member.Id, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(ownerResult.IsSuccess);
        Assert.Equal(nameof(Role.Owner), ownerResult.Value.UserRole);

        Assert.True(memberResult.IsSuccess);
        Assert.Equal(nameof(Role.Member), memberResult.Value.UserRole);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnassignedCard_ReturnsCardWithNullAssignee()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "A card", Position = 1 }));

        // Act
        var result = await UseBoardServiceAsync(service =>
            service.GetByIdAsync(board.Id, owner.Id, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(result.IsSuccess);
        var card = Assert.Single(result.Value.Columns.Single().Cards);
        Assert.Null(card.AssignedTo);
    }
}

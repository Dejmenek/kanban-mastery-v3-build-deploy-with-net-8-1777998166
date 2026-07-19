using Kanban.API.DTOs.Boards.Cards;
using Kanban.API.Models;
using System.Net;
using System.Net.Http.Json;

namespace Kanban.API.IntegrationTests;

public class CardEndpointsTests(IntegrationTestWebAppFactory<Program> factory)
    : IntegrationTestBase(factory), IClassFixture<IntegrationTestWebAppFactory<Program>>
{
    [Fact]
    public async Task CreateCard_AsOwner_ReturnsCreated()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));

        var request = new CreateCardRequest("New card", "Some description", column.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/cards",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(request.Title, body.Title);
        Assert.Equal(request.Description, body.Description);
    }

    [Fact]
    public async Task CreateCard_AsMember_ReturnsCreated()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));

        var memberEmail = "member@example.com";
        var memberPassword = "Test123!";
        var member = await CreateUserAsync(memberEmail, memberPassword);
        await UseDbContextAsync(context =>
            BoardTestHelper.SeedBoardMemberAsync(context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));
        await AuthenticateAsAsync(memberEmail, memberPassword);

        var request = new CreateCardRequest("New card", null, column.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/cards",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(request.Title, body.Title);
        Assert.Equal(request.Description, body.Description);
    }

    [Fact]
    public async Task CreateCard_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));

        var nonMemberEmail = "nonmember@example.com";
        var nonMemberPassword = "Test123!";
        await CreateUserAsync(nonMemberEmail, nonMemberPassword);
        await AuthenticateAsAsync(nonMemberEmail, nonMemberPassword);

        var request = new CreateCardRequest("New card", null, column.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/cards",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCard_WithInvalidTitle_ReturnsBadRequest()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));

        var request = new CreateCardRequest("   ", null, column.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/cards",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCard_WithNonExistentColumn_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentColumnId = 999;
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var request = new CreateCardRequest("New card", null, nonExistentColumnId);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/cards",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCard_AsMember_ReturnsOkAndUpdates()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Original", Position = 1 }));

        var memberEmail = "member@example.com";
        var memberPassword = "Test123!";
        var member = await CreateUserAsync(memberEmail, memberPassword);
        await UseDbContextAsync(context =>
            BoardTestHelper.SeedBoardMemberAsync(context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));
        await AuthenticateAsAsync(memberEmail, memberPassword);

        var request = new UpdateCardRequest("Updated title", "Updated description", column.Id);

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/cards/{card.Id}",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(request.Title, body.Title);
        Assert.Equal(request.Description, body.Description);
    }

    [Fact]
    public async Task UpdateCard_MoveToAnotherColumn_ReturnsOk()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
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
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/cards/{card.Id}",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(request.Title, body.Title);
        Assert.Equal(request.Description, body.Description);
    }

    [Fact]
    public async Task UpdateCard_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Original", Position = 1 }));

        var nonMemberEmail = "nonmember@example.com";
        var nonMemberPassword = "Test123!";
        await CreateUserAsync(nonMemberEmail, nonMemberPassword);
        await AuthenticateAsAsync(nonMemberEmail, nonMemberPassword);

        var request = new UpdateCardRequest("Updated title", null, column.Id);

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/cards/{card.Id}",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCard_WithInvalidTitle_ReturnsBadRequest()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Original", Position = 1 }));

        var request = new UpdateCardRequest("   ", null, column.Id);

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/cards/{card.Id}",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCard_WithNonExistentCard_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentCardId = 999;
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));

        var request = new UpdateCardRequest("Updated title", null, column.Id);

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/cards/{nonExistentCardId}",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCard_MoveToNonExistentColumn_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentColumnId = 999;
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Original", Position = 1 }));

        var request = new UpdateCardRequest("Original", null, nonExistentColumnId);

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/cards/{card.Id}",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCard_AsMember_ReturnsNoContent()
    {
        // Arrange
        var owner = await CreateUserAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Delete me", Position = 1 }));

        var memberEmail = "member@example.com";
        var memberPassword = "Test123!";
        var member = await CreateUserAsync(memberEmail, memberPassword);
        await UseDbContextAsync(context =>
            BoardTestHelper.SeedBoardMemberAsync(context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));
        await AuthenticateAsAsync(memberEmail, memberPassword);

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/cards/{card.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCard_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "To Do", Position = 1 }));
        var card = await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "Original", Position = 1 }));

        var nonMemberEmail = "nonmember@example.com";
        var nonMemberPassword = "Test123!";
        await CreateUserAsync(nonMemberEmail, nonMemberPassword);
        await AuthenticateAsAsync(nonMemberEmail, nonMemberPassword);

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/cards/{card.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCard_WithNonExistentCard_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentCardId = 999;
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/cards/{nonExistentCardId}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

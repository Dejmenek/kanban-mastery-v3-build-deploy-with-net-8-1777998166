using Kanban.API.DTOs.Boards.Columns;
using Kanban.API.Models;
using System.Net;
using System.Net.Http.Json;

namespace Kanban.API.IntegrationTests;

public class ColumnEndpointsTests(IntegrationTestWebAppFactory<Program> factory)
    : IntegrationTestBase(factory), IClassFixture<IntegrationTestWebAppFactory<Program>>
{
    [Fact]
    public async Task CreateColumn_AsMember_ReturnsCreated()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var request = new CreateColumnRequest("To Do", "Tasks not started", null);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/columns",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ColumnResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(request.Title, body.Title);
        Assert.Equal(request.Description, body.Description);
    }

    [Fact]
    public async Task CreateColumn_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var nonMemberEmail = "nonmember@example.com";
        var nonMemberPassword = "Test123!";
        await CreateUserAsync(nonMemberEmail, nonMemberPassword);
        await AuthenticateAsAsync(nonMemberEmail, nonMemberPassword);

        var request = new CreateColumnRequest("To Do", null, null);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/columns",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateColumn_WithInvalidTitle_ReturnsBadRequest()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var request = new CreateColumnRequest("   ", null, null);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/columns",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateColumn_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Original", Position = 1 }));

        var nonMemberEmail = "nonmember@example.com";
        var nonMemberPassword = "Test123!";
        await CreateUserAsync(nonMemberEmail, nonMemberPassword);
        await AuthenticateAsAsync(nonMemberEmail, nonMemberPassword);

        var request = new UpdateColumnRequest("Updated Title", null);

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/columns/{column.Id}",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateColumn_WithInvalidTitle_ReturnsBadRequest()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Original", Position = 1 }));

        var request = new UpdateColumnRequest("   ", null);

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/columns/{column.Id}",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateColumn_WithNonExistentColumn_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentColumnId = 999;
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var request = new UpdateColumnRequest("Updated Title", null);

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/columns/{nonExistentColumnId}",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateColumn_AsMember_ReturnsOkAndUpdatesColumn()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Original", Position = 1 }));

        var request = new UpdateColumnRequest("Updated Title", "Updated description");

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/columns/{column.Id}",
            request,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ColumnResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(request.Title, body.Title);
        Assert.Equal(request.Description, body.Description);
    }

    [Fact]
    public async Task DeleteColumn_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Original", Position = 1 }));

        var nonMemberEmail = "nonmember@example.com";
        var nonMemberPassword = "Test123!";
        await CreateUserAsync(nonMemberEmail, nonMemberPassword);
        await AuthenticateAsAsync(nonMemberEmail, nonMemberPassword);

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/columns/{column.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteColumn_WithNonExistentColumn_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentColumnId = 999;
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/columns/{nonExistentColumnId}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteColumn_AsMember_ReturnsNoContent()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Original", Position = 1 }));

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/columns/{column.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteColumn_WithCards_ReturnsConflict()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        var column = await UseDbContextAsync(context =>
            BoardTestHelper.SeedColumnAsync(context, new Column { BoardId = board.Id, Title = "Original", Position = 1 }));
        await UseDbContextAsync(context =>
            BoardTestHelper.SeedCardAsync(context, new Card { ColumnId = column.Id, Title = "A card", Position = 1 }));

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/columns/{column.Id}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}

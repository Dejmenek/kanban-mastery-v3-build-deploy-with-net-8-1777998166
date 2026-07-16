using Kanban.API.DTOs.Boards.Columns;
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
}

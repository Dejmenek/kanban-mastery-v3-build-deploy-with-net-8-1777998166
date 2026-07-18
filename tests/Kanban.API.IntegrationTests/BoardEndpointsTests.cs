using Kanban.API.DTOs.Boards;
using Kanban.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace Kanban.API.IntegrationTests;

public class BoardEndpointsTests(IntegrationTestWebAppFactory<Program> factory) : IntegrationTestBase(factory), IClassFixture<IntegrationTestWebAppFactory<Program>>
{
    [Fact]
    public async Task CreateBoard_WithValidRequest_CreatesBoardAndBoardMember()
    {
        // Arrange
        var boardName = "Test Board";
        var user = await CreateUserAndAuthenticateAsync("test@example.com", "Test123!");

        // Act
        var response = await Client.PostAsJsonAsync("/api/boards", new { Name = boardName }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<BoardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(content);

        var membership = await UseDbContextAsync(context => context.BoardsMemberships
            .Include(bm => bm.Board)
            .FirstOrDefaultAsync(bm => bm.BoardId == content.Id && bm.MemberId == user.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(membership);
        Assert.Equal(boardName, membership.Board.Name);
        Assert.Equal(Role.Owner, membership.Role);
    }

    [Fact]
    public async Task AddMember_AsBoardOwner_AddsMemberAndReturnsCreated()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var memberEmail = "member@example.com";
        var newMember = await CreateUserAsync(memberEmail, "Test123!");

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/members",
            new { Email = memberEmail },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BoardMemberResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(newMember.Id, body.MemberId);
        Assert.Equal(newMember.UserName, body.UserName);
        Assert.Equal(nameof(Role.Member), body.Role);

        var membership = await UseDbContextAsync(context => context.BoardsMemberships
            .FirstOrDefaultAsync(bm => bm.BoardId == board.Id && bm.MemberId == newMember.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(membership);
        Assert.Equal(Role.Member, membership.Role);
    }

    [Fact]
    public async Task AddMember_AsNonOwner_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var nonOwnerEmail = "nonowner@example.com";
        var nonOwnerPassword = "Test123!";
        await CreateUserAsync(nonOwnerEmail, nonOwnerPassword);

        var candidateEmail = "candidate@example.com";
        await CreateUserAsync(candidateEmail, "Test123!");

        await AuthenticateAsAsync(nonOwnerEmail, nonOwnerPassword);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/members",
            new { Email = candidateEmail },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_WithoutUserIdOrEmail_ReturnsBadRequest()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/members",
            new { },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_WithNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/members",
            new { Email = "doesnotexist@example.com" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_WhenUserIsAlreadyAMember_ReturnsConflict()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var memberEmail = "member@example.com";
        var member = await CreateUserAsync(memberEmail, "Test123!");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/members",
            new { Email = memberEmail },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WhenOwnerRequestsBoard_ReturnsBoardDetails()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var column = await UseDbContextAsync(context => BoardTestHelper.SeedColumnAsync(
            context, new Column { BoardId = board.Id, Title = "To Do", Position = 0 }));
        var card = await UseDbContextAsync(context => BoardTestHelper.SeedCardAsync(
            context, new Card { ColumnId = column.Id, Title = "Test Card", Position = 0 }));

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BoardDetailsResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(board.Id, body.Id);
        Assert.Equal(board.Name, body.Name);

        var returnedColumn = Assert.Single(body.Columns);
        Assert.Equal(column.Id, returnedColumn.Id);
        Assert.Equal(column.Title, returnedColumn.Title);

        var returnedCard = Assert.Single(returnedColumn.Cards);
        Assert.Equal(card.Id, returnedCard.Id);
        Assert.Equal(card.Title, returnedCard.Title);
    }

    [Fact]
    public async Task GetById_WhenMemberRequestsBoard_ReturnsBoardDetails()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var memberEmail = "member@example.com";
        var memberPassword = "Test123!";
        var member = await CreateUserAsync(memberEmail, memberPassword);
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        await AuthenticateAsAsync(memberEmail, memberPassword);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BoardDetailsResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(board.Id, body.Id);
        Assert.Equal(board.Name, body.Name);
    }

    [Fact]
    public async Task GetById_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var nonMemberEmail = "nonmember@example.com";
        var nonMemberPassword = "Test123!";
        await CreateUserAsync(nonMemberEmail, nonMemberPassword);

        await AuthenticateAsAsync(nonMemberEmail, nonMemberPassword);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_AsOwner_ReturnsOkAndUpdatesBoard()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var request = new UpdateBoardRequest("Updated Name", "Updated description");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BoardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(request.Name, body.Name);
        Assert.Equal(request.Description, body.Description);
    }

    [Fact]
    public async Task UpdateBoard_AsMember_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var memberEmail = "member@example.com";
        var memberPassword = "Test123!";
        var member = await CreateUserAsync(memberEmail, memberPassword);
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        await AuthenticateAsAsync(memberEmail, memberPassword);

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}", new UpdateBoardRequest("Hacked Name", null), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_AsOwner_ReturnsNoContent()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_AsMember_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var memberEmail = "member@example.com";
        var memberPassword = "Test123!";
        var member = await CreateUserAsync(memberEmail, memberPassword);
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        await AuthenticateAsAsync(memberEmail, memberPassword);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

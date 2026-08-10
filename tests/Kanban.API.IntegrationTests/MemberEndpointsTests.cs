using Kanban.API.Common;
using Kanban.API.DTOs.Boards;
using Kanban.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace Kanban.API.IntegrationTests;

public class MemberEndpointsTests(IntegrationTestWebAppFactory<Program> factory) : IntegrationTestBase(factory), IClassFixture<IntegrationTestWebAppFactory<Program>>
{
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
        Assert.Equal(newMember.Email, body.Email);
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
    public async Task GetAllMembers_AsMember_ReturnsOkWithMembers()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var member = await CreateUserAsync("member@example.com", "Test123!");
        await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
            context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/members", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CursorPagedResponse<BoardMemberResponse>>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);
        Assert.Null(body.NextCursor);
        Assert.Contains(body.Items, m => m.MemberId == owner.Id);
        Assert.Contains(body.Items, m => m.MemberId == member.Id);
    }

    [Fact]
    public async Task GetAllMembers_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/members", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllMembers_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        var nonMemberEmail = "nonmember@example.com";
        var nonMemberPassword = "Test123!";
        await CreateUserAsync(nonMemberEmail, nonMemberPassword);

        await AuthenticateAsAsync(nonMemberEmail, nonMemberPassword);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/members", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAllMembers_WithNonExistentBoard_ReturnsForbidden()
    {
        // Arrange
        await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");

        // Act
        var response = await Client.GetAsync("/api/boards/999999/members", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAllMembers_WithInvalidCursor_ReturnsBadRequest()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/members?cursor=not-valid-base64!!!", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAllMembers_WithPagination_WalksAllPagesWithoutDuplicates()
    {
        // Arrange
        var owner = await CreateUserAndAuthenticateAsync("owner@example.com", "Test123!");
        var board = await UseDbContextAsync(context => BoardTestHelper.SeedBoardAsync(context, owner.Id));

        for (var i = 0; i < 4; i++)
        {
            var member = await CreateUserAsync($"member{i}@example.com", "Test123!");
            await UseDbContextAsync(context => BoardTestHelper.SeedBoardMemberAsync(
                context, new BoardMember { BoardId = board.Id, MemberId = member.Id, Role = Role.Member }));
        }

        // Act
        var collectedIds = new List<string>();
        string? cursor = null;
        do
        {
            var url = $"/api/boards/{board.Id}/members?pageSize=2" + (cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}");
            var response = await Client.GetAsync(url, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<CursorPagedResponse<BoardMemberResponse>>(TestContext.Current.CancellationToken);
            Assert.NotNull(body);
            Assert.True(body.Items.Count <= 2);
            collectedIds.AddRange(body.Items.Select(m => m.MemberId));
            cursor = body.NextCursor;
        } while (cursor is not null);

        // Assert
        Assert.Equal(5, collectedIds.Count);
        Assert.Equal(collectedIds.Distinct().Count(), collectedIds.Count);
    }
}

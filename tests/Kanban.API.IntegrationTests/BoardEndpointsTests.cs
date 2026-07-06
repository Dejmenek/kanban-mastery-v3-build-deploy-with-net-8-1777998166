using Kanban.API.Data;
using Kanban.API.DTOs.Boards;
using Kanban.API.Models;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Kanban.API.IntegrationTests;

public class BoardEndpointsTests : IntegrationTestBase, IClassFixture<IntegrationTestWebAppFactory<Program>>
{
    private readonly HttpClient _client;

    public BoardEndpointsTests(IntegrationTestWebAppFactory<Program> factory) : base(factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateBoard_WithValidRequest_CreatesBoardAndBoardMember()
    {
        // Arrange
        var email = "test@example.com";
        var userName = "test@example.com";
        var password = "Test123!";
        var boardName = "Test Board";
        ApplicationUser user;
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            user = new ApplicationUser { UserName = userName, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user, password);
        }

        var loginResponse = await _client.PostAsJsonAsync("/login", new { email, password }, TestContext.Current.CancellationToken);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/boards", new { Name = boardName }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<BoardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(content);

        using var assertScope = Factory.Services.CreateScope();
        var context = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var membership = await context.BoardsMemberships
            .Include(bm => bm.Board)
            .FirstOrDefaultAsync(bm => bm.BoardId == content.Id && bm.MemberId == user.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(membership);
        Assert.Equal(boardName, membership.Board.Name);
        Assert.Equal(Role.Owner, membership.Role);
    }

    [Fact]
    public async Task AddMember_AsBoardOwner_AddsMemberAndReturnsCreated()
    {
        // Arrange
        var ownerEmail = "owner@example.com";
        var ownerPassword = "Test123!";
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = new ApplicationUser { UserName = ownerEmail, Email = ownerEmail, EmailConfirmed = true };
            await userManager.CreateAsync(owner, ownerPassword);
        }

        var ownerLoginResponse = await _client.PostAsJsonAsync("/login", new { email = ownerEmail, password = ownerPassword }, TestContext.Current.CancellationToken);
        var ownerTokens = await ownerLoginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens!.AccessToken);

        var createBoardResponse = await _client.PostAsJsonAsync("/api/boards", new { Name = "Test Board" }, TestContext.Current.CancellationToken);
        var board = await createBoardResponse.Content.ReadFromJsonAsync<BoardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(board);

        var memberEmail = "member@example.com";
        ApplicationUser newMember;
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            newMember = new ApplicationUser { UserName = memberEmail, Email = memberEmail, EmailConfirmed = true };
            await userManager.CreateAsync(newMember, "Test123!");
        }

        // Act
        var response = await _client.PostAsJsonAsync(
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

        using var assertScope = Factory.Services.CreateScope();
        var context = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var membership = await context.BoardsMemberships
            .FirstOrDefaultAsync(bm => bm.BoardId == board.Id && bm.MemberId == newMember.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(membership);
        Assert.Equal(Role.Member, membership.Role);
    }

    [Fact]
    public async Task AddMember_AsNonOwner_ReturnsForbidden()
    {
        // Arrange
        var ownerEmail = "owner@example.com";
        var ownerPassword = "Test123!";
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = new ApplicationUser { UserName = ownerEmail, Email = ownerEmail, EmailConfirmed = true };
            await userManager.CreateAsync(owner, ownerPassword);
        }

        var ownerLoginResponse = await _client.PostAsJsonAsync("/login", new { email = ownerEmail, password = ownerPassword }, TestContext.Current.CancellationToken);
        var ownerTokens = await ownerLoginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens!.AccessToken);

        var createBoardResponse = await _client.PostAsJsonAsync("/api/boards", new { Name = "Test Board" }, TestContext.Current.CancellationToken);
        var board = await createBoardResponse.Content.ReadFromJsonAsync<BoardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(board);

        var nonOwnerEmail = "nonowner@example.com";
        var nonOwnerPassword = "Test123!";
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var nonOwner = new ApplicationUser { UserName = nonOwnerEmail, Email = nonOwnerEmail, EmailConfirmed = true };
            await userManager.CreateAsync(nonOwner, nonOwnerPassword);
        }

        var candidateEmail = "candidate@example.com";
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var candidate = new ApplicationUser { UserName = candidateEmail, Email = candidateEmail, EmailConfirmed = true };
            await userManager.CreateAsync(candidate, "Test123!");
        }

        var nonOwnerLoginResponse = await _client.PostAsJsonAsync("/login", new { email = nonOwnerEmail, password = nonOwnerPassword }, TestContext.Current.CancellationToken);
        var nonOwnerTokens = await nonOwnerLoginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nonOwnerTokens!.AccessToken);

        // Act
        var response = await _client.PostAsJsonAsync(
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
        var ownerEmail = "owner@example.com";
        var ownerPassword = "Test123!";
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = new ApplicationUser { UserName = ownerEmail, Email = ownerEmail, EmailConfirmed = true };
            await userManager.CreateAsync(owner, ownerPassword);
        }

        var ownerLoginResponse = await _client.PostAsJsonAsync("/login", new { email = ownerEmail, password = ownerPassword }, TestContext.Current.CancellationToken);
        var ownerTokens = await ownerLoginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens!.AccessToken);

        var createBoardResponse = await _client.PostAsJsonAsync("/api/boards", new { Name = "Test Board" }, TestContext.Current.CancellationToken);
        var board = await createBoardResponse.Content.ReadFromJsonAsync<BoardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(board);

        // Act
        var response = await _client.PostAsJsonAsync(
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
        var ownerEmail = "owner@example.com";
        var ownerPassword = "Test123!";
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = new ApplicationUser { UserName = ownerEmail, Email = ownerEmail, EmailConfirmed = true };
            await userManager.CreateAsync(owner, ownerPassword);
        }

        var ownerLoginResponse = await _client.PostAsJsonAsync("/login", new { email = ownerEmail, password = ownerPassword }, TestContext.Current.CancellationToken);
        var ownerTokens = await ownerLoginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens!.AccessToken);

        var createBoardResponse = await _client.PostAsJsonAsync("/api/boards", new { Name = "Test Board" }, TestContext.Current.CancellationToken);
        var board = await createBoardResponse.Content.ReadFromJsonAsync<BoardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(board);

        // Act
        var response = await _client.PostAsJsonAsync(
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
        var ownerEmail = "owner@example.com";
        var ownerPassword = "Test123!";
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = new ApplicationUser { UserName = ownerEmail, Email = ownerEmail, EmailConfirmed = true };
            await userManager.CreateAsync(owner, ownerPassword);
        }

        var ownerLoginResponse = await _client.PostAsJsonAsync("/login", new { email = ownerEmail, password = ownerPassword }, TestContext.Current.CancellationToken);
        var ownerTokens = await ownerLoginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens!.AccessToken);

        var createBoardResponse = await _client.PostAsJsonAsync("/api/boards", new { Name = "Test Board" }, TestContext.Current.CancellationToken);
        var board = await createBoardResponse.Content.ReadFromJsonAsync<BoardResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(board);

        var memberEmail = "member@example.com";
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existingMember = new ApplicationUser { UserName = memberEmail, Email = memberEmail, EmailConfirmed = true };
            await userManager.CreateAsync(existingMember, "Test123!");
        }

        var firstResponse = await _client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/members",
            new { Email = memberEmail },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act
        var secondResponse = await _client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/members",
            new { Email = memberEmail },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }
}

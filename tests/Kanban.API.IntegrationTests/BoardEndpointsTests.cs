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
}

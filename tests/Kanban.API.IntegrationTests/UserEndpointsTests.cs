using Kanban.API.DTOs.Users;
using Kanban.API.Models;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Kanban.API.IntegrationTests;

public class UserEndpointsTests : IntegrationTestBase, IClassFixture<IntegrationTestWebAppFactory<Program>>
{
    private readonly HttpClient _client;

    public UserEndpointsTests(IntegrationTestWebAppFactory<Program> factory) : base(factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCurrentUserProfile_WithValidToken_ReturnsOk()
    {
        // Arrange
        var email = "test@example.com";
        var userName = "test@example.com";
        var password = "Test123!";
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
        var response = await _client.GetAsync("/api/users/me", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CurrentUserProfileResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.Id);
        Assert.Equal(user.UserName, body.UserName);
        Assert.Equal(user.Email, body.Email);
    }

    [Fact]
    public async Task GetCurrentUserProfile_WithInvalidToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/users/me", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUserProfile_WithNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var email = "test@example.com";
        var userName = "test@example.com";
        var password = "Test123!";
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = userName, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user, password);

            var loginResponse = await _client.PostAsJsonAsync("/login", new { email, password }, TestContext.Current.CancellationToken);
            var tokens = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

            await userManager.DeleteAsync(user);
        }

        // Act
        var response = await _client.GetAsync("/api/users/me", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

using Kanban.API.DTOs.Users;
using Kanban.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Kanban.API.IntegrationTests;

public class UserEndpointsTests(IntegrationTestWebAppFactory<Program> factory) : IntegrationTestBase(factory), IClassFixture<IntegrationTestWebAppFactory<Program>>
{
    [Fact]
    public async Task GetCurrentUserProfile_WithValidToken_ReturnsOk()
    {
        // Arrange
        var user = await CreateUserAndAuthenticateAsync("test@example.com", "Test123!");

        // Act
        var response = await Client.GetAsync("/api/users/me", TestContext.Current.CancellationToken);

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
        var response = await Client.GetAsync("/api/users/me", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUserProfile_WithNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var user = await CreateUserAndAuthenticateAsync("test@example.com", "Test123!");

        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await userManager.DeleteAsync(user);
        }

        // Act
        var response = await Client.GetAsync("/api/users/me", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

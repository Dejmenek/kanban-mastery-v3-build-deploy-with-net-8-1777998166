using Kanban.API.Models;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Kanban.API.IntegrationTests;

public class AuthTests(IntegrationTestWebAppFactory<Program> factory) : IntegrationTestBase(factory), IClassFixture<IntegrationTestWebAppFactory<Program>>
{
    [Fact]
    public async Task Register_WithValidData_ReturnsOk()
    {
        // Arrange
        var data = new
        {
            email = "test@example.com",
            password = "Test123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/register", data, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidData_ReturnsOkWithAccessToken()
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
        }

        var data = new
        {
            email,
            password
        };

        // Act
        var response = await Client.PostAsJsonAsync("/login", data, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.AccessToken));
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
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
        }

        var data = new
        {
            email,
            password
        };

        // Act
        var response = await Client.PostAsJsonAsync("/register", data, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("DuplicateEmail"));
    }
}
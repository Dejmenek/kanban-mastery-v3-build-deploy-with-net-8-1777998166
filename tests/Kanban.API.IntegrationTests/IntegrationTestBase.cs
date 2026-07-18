using Kanban.API.Data;
using Kanban.API.Models;
using Kanban.API.Services;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Kanban.API.IntegrationTests;

public abstract class IntegrationTestBase(IntegrationTestWebAppFactory<Program> factory) : IAsyncLifetime
{
    protected readonly IntegrationTestWebAppFactory<Program> Factory = factory;
    protected readonly HttpClient Client = factory.CreateClient();

    public async ValueTask InitializeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected async Task<ApplicationUser> CreateUserAsync(string email, string password, string? userName = null)
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = userName ?? email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, password);
        return user;
    }

    protected async Task AuthenticateAsAsync(string email, string password)
    {
        var loginResponse = await Client.PostAsJsonAsync("/login", new { email, password }, TestContext.Current.CancellationToken);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(TestContext.Current.CancellationToken);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    protected async Task<ApplicationUser> CreateUserAndAuthenticateAsync(string email, string password, string? userName = null)
    {
        var user = await CreateUserAsync(email, password, userName);
        await AuthenticateAsAsync(email, password);
        return user;
    }

    protected async Task UseDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(context);
    }

    protected async Task<TResult> UseDbContextAsync<TResult>(Func<ApplicationDbContext, Task<TResult>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(context);
    }

    protected async Task<TResult> UseColumnServiceAsync<TResult>(Func<IColumnService, Task<TResult>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IColumnService>();
        return await action(service);
    }

    protected async Task<TResult> UseBoardServiceAsync<TResult>(Func<IBoardService, Task<TResult>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBoardService>();
        return await action(service);
    }
}

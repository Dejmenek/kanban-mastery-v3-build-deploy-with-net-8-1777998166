using Kanban.API.Data;
using Microsoft.Extensions.DependencyInjection;

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
}

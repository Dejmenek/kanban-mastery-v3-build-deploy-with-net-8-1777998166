using Kanban.API.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Kanban.API.IntegrationTests;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IntegrationTestWebAppFactory<Program> Factory;

    protected IntegrationTestBase(IntegrationTestWebAppFactory<Program> factory)
    {
        Factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

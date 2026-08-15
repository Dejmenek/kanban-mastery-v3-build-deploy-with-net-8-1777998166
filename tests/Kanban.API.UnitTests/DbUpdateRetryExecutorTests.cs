using Kanban.API.Common;
using Kanban.API.Data;
using Kanban.API.Models;
using Kanban.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Kanban.API.UnitTests;

public class DbUpdateRetryExecutorTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceedsOnFirstAttempt_ReturnsResultWithoutRetrying()
    {
        // Arrange
        using var context = CreateContext();
        var executor = new DbUpdateRetryExecutor(context);
        var attempts = 0;

        // Act
        var result = await executor.ExecuteAsync(
            maxAttempts: 3,
            operation: () =>
            {
                attempts++;
                return Task.FromResult(Result.Success("done"));
            },
            isRetryable: _ => true,
            onExhausted: () => Result.Failure<string>(Error.General),
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("done", result.Value);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetryableExceptionThrownFewerTimesThanMaxAttempts_RetriesAndEventuallySucceeds()
    {
        // Arrange
        using var context = CreateContext();
        var executor = new DbUpdateRetryExecutor(context);
        var attempts = 0;

        // Act
        var result = await executor.ExecuteAsync(
            maxAttempts: 3,
            operation: () =>
            {
                attempts++;
                if (attempts < 3) throw new DbUpdateException("conflict");
                return Task.FromResult(Result.Success("done"));
            },
            isRetryable: _ => true,
            onExhausted: () => Result.Failure<string>(Error.General),
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("done", result.Value);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetryableExceptionThrownOnEveryAttempt_ReturnsOnExhaustedResultAfterMaxAttempts()
    {
        // Arrange
        using var context = CreateContext();
        var executor = new DbUpdateRetryExecutor(context);
        var attempts = 0;

        // Act
        var result = await executor.ExecuteAsync(
            maxAttempts: 3,
            operation: () =>
            {
                attempts++;
                throw new DbUpdateException("conflict");
            },
            isRetryable: _ => true,
            onExhausted: () => Result.Failure<string>(Error.General),
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(Error.General, result.Error);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WithMaxAttemptsOfOne_ReturnsOnExhaustedAfterSingleFailedAttempt()
    {
        // Arrange
        using var context = CreateContext();
        var executor = new DbUpdateRetryExecutor(context);
        var attempts = 0;

        // Act
        var result = await executor.ExecuteAsync(
            maxAttempts: 1,
            operation: () =>
            {
                attempts++;
                throw new DbUpdateException("conflict");
            },
            isRetryable: _ => true,
            onExhausted: () => Result.Failure<string>(Error.General),
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExceptionIsNotRetryable_PropagatesExceptionWithoutRetrying()
    {
        // Arrange
        using var context = CreateContext();
        var executor = new DbUpdateRetryExecutor(context);
        var attempts = 0;

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => executor.ExecuteAsync(
            maxAttempts: 3,
            operation: () =>
            {
                attempts++;
                throw new DbUpdateException("not retryable");
            },
            isRetryable: _ => false,
            onExhausted: () => Result.Failure<string>(Error.General),
            cancellationToken: CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_PassesThrownExceptionInstanceToIsRetryablePredicate()
    {
        // Arrange
        using var context = CreateContext();
        var executor = new DbUpdateRetryExecutor(context);
        var thrown = new DbUpdateException("specific");
        DbUpdateException? received = null;

        // Act
        await Assert.ThrowsAsync<DbUpdateException>(() => executor.ExecuteAsync(
            maxAttempts: 1,
            operation: () => throw thrown,
            isRetryable: ex =>
            {
                received = ex;
                return false;
            },
            onExhausted: () => Result.Failure<string>(Error.General),
            cancellationToken: CancellationToken.None));

        // Assert
        Assert.Same(thrown, received);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetrying_ClearsChangeTrackerBeforeNextAttempt()
    {
        // Arrange
        using var context = CreateContext();
        context.Boards.Add(new Board { Name = "Tracked" });
        Assert.NotEmpty(context.ChangeTracker.Entries());

        var executor = new DbUpdateRetryExecutor(context);
        var attempts = 0;

        // Act
        await executor.ExecuteAsync(
            maxAttempts: 2,
            operation: () =>
            {
                attempts++;
                if (attempts == 1) throw new DbUpdateException("conflict");
                return Task.FromResult(Result.Success("done"));
            },
            isRetryable: _ => true,
            onExhausted: () => Result.Failure<string>(Error.General),
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Empty(context.ChangeTracker.Entries());
    }
}

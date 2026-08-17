using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Threading.RateLimiting;

namespace Kanban.API.IntegrationTests;

public class RateLimitingTests(IntegrationTestWebAppFactory<Program> factory) : IClassFixture<IntegrationTestWebAppFactory<Program>>
{
    private const int TokenLimit = 2;

    private HttpClient CreateRateLimitedClient()
    {
        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<RateLimiterOptions>(options =>
                {
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                        RateLimitPartition.GetTokenBucketLimiter(
                            partitionKey: "rate-limit-test",
                            factory: _ => new TokenBucketRateLimiterOptions
                            {
                                TokenLimit = TokenLimit,
                                TokensPerPeriod = 1,
                                ReplenishmentPeriod = TimeSpan.FromHours(1),
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = 0,
                                AutoReplenishment = true
                            }));
                });
            });
        }).CreateClient();

        return client;
    }

    [Fact]
    public async Task ExceedingRateLimit_Returns429WithRetryAfterHeader()
    {
        // Arrange
        using var client = CreateRateLimitedClient();

        // Act
        for (var i = 0; i < TokenLimit; i++)
        {
            var response = await client.GetAsync("/api/users/me", TestContext.Current.CancellationToken);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var rejectedResponse = await client.GetAsync("/api/users/me", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);

        var hasRetryAfterHeader = rejectedResponse.Headers.TryGetValues("Retry-After", out var retryAfterValues);
        Assert.True(hasRetryAfterHeader);

        var retryAfterValue = Assert.Single(retryAfterValues!);
        Assert.True(int.TryParse(retryAfterValue, out var retryAfterSeconds));
        Assert.True(retryAfterSeconds >= 0);
    }
}

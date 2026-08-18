using System.Net;
using System.Text;
using Disney.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Disney.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public async Task QueueTimesClient_MapsExternalPayload()
    {
        using var httpClient = new HttpClient(new StubHandler(
            HttpStatusCode.OK,
            """
            {
              "lands": [{
                "id": 10,
                "name": "Tomorrowland",
                "rides": [{
                  "id": 20,
                  "name": "Space Mountain",
                  "is_open": true,
                  "wait_time": 35,
                  "last_updated": "2026-08-18T19:05:30Z"
                }]
              }],
              "rides": []
            }
            """))
        {
            BaseAddress = new Uri("https://queue-times.test")
        };
        var client = new QueueTimesClient(
            httpClient,
            NullLogger<QueueTimesClient>.Instance);

        var snapshot = await client.GetQueueTimesForParkAsync(6, CancellationToken.None);

        var ride = Assert.Single(Assert.Single(snapshot.Lands).Rides);
        Assert.Equal(20, ride.SourceRideId);
        Assert.Equal(35, ride.WaitMinutes);
    }

    [Fact]
    public async Task QueueTimesClient_RejectsEmptyResponse()
    {
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.OK, "null"))
        {
            BaseAddress = new Uri("https://queue-times.test")
        };

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new QueueTimesClient(
                    httpClient,
                    NullLogger<QueueTimesClient>.Instance)
                .GetQueueTimesForParkAsync(6, CancellationToken.None));
    }

    [Fact]
    public async Task QueueTimesClient_RejectsPayloadWithoutRides()
    {
        using var httpClient = new HttpClient(new StubHandler(
            HttpStatusCode.OK,
            """{"lands":[],"rides":[]}"""))
        {
            BaseAddress = new Uri("https://queue-times.test")
        };

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new QueueTimesClient(
                    httpClient,
                    NullLogger<QueueTimesClient>.Instance)
                .GetQueueTimesForParkAsync(6, CancellationToken.None));
    }

    [Fact]
    public void InitialMigration_DefinesAppendOnlyIdentityAndReadIndexes()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "Migrations",
            "001_initial_schema.sql");
        var sql = File.ReadAllText(Path.GetFullPath(path));

        Assert.Contains("(attraction_id, observed_at)", sql);
        Assert.Contains("observed_slot_minutes smallint NOT NULL", sql);
        Assert.Contains("USING brin (observed_at)", sql);
        Assert.Contains("duration_minutes", sql);
        Assert.DoesNotContain("IF NOT EXISTS public.parks", sql);
        Assert.DoesNotContain("source_last_updated", sql);
        Assert.DoesNotContain(
            "UNIQUE (attraction_id, observed_local_date, observed_slot_minutes)",
            sql);
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string content)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }
}

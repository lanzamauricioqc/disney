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
        var queueTimesClient = new QueueTimesClient(
            httpClient,
            NullLogger<QueueTimesClient>.Instance);

        var snapshot = await queueTimesClient.GetQueueTimesForParkAsync(
            6,
            CancellationToken.None);

        var rideSnapshot = Assert.Single(Assert.Single(snapshot.Lands).Rides);
        Assert.Equal(20, rideSnapshot.SourceRideId);
        Assert.Equal(35, rideSnapshot.WaitMinutes);
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
        var migrationPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "Migrations",
            "001_initial_schema.sql");
        var migrationSql = File.ReadAllText(Path.GetFullPath(migrationPath));

        Assert.Contains("(attraction_id, observed_at)", migrationSql);
        Assert.Contains("observed_slot_minutes smallint NOT NULL", migrationSql);
        Assert.Contains("USING brin (observed_at)", migrationSql);
        Assert.Contains("duration_minutes", migrationSql);
        Assert.DoesNotContain("IF NOT EXISTS public.parks", migrationSql);
        Assert.DoesNotContain("source_last_updated", migrationSql);
        Assert.DoesNotContain(
            "UNIQUE (attraction_id, observed_local_date, observed_slot_minutes)",
            migrationSql);
    }

    [Fact]
    public void AnalyticsReader_UsesWeekdayHourlyAggregates()
    {
        var analyticsReaderPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "PostgreSqlQueueAnalyticsReader.cs");
        var analyticsReaderSourceCode =
            File.ReadAllText(Path.GetFullPath(analyticsReaderPath));

        Assert.Contains("percentile_cont(0.5)", analyticsReaderSourceCode);
        Assert.Contains("observed_day_of_week", analyticsReaderSourceCode);
        Assert.Contains("observed_local_hour", analyticsReaderSourceCode);
        Assert.Contains("ClosedPercentage", analyticsReaderSourceCode);
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

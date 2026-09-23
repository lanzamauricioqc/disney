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
        Assert.Contains("latitude numeric(9,6)", migrationSql);
        Assert.Contains("longitude numeric(9,6)", migrationSql);
        Assert.Contains("latitude BETWEEN -90 AND 90", migrationSql);
        Assert.Contains("longitude BETWEEN -180 AND 180", migrationSql);
        Assert.DoesNotContain("IF NOT EXISTS public.parks", migrationSql);
        Assert.DoesNotContain("source_last_updated", migrationSql);
        Assert.DoesNotContain(
            "UNIQUE (attraction_id, observed_local_date, observed_slot_minutes)",
            migrationSql);
    }

    [Fact]
    public void UtcComponentsMigration_BackfillsFromUtcInstant()
    {
        var migrationPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "Migrations",
            "003_add_observation_utc_components.sql");
        var migrationSql = File.ReadAllText(Path.GetFullPath(migrationPath));

        Assert.Contains("observed_utc_date date", migrationSql);
        Assert.Contains("observed_utc_time time", migrationSql);
        Assert.Contains("observed_utc_slot_minutes smallint", migrationSql);
        Assert.Contains("observed_at AT TIME ZONE 'UTC'", migrationSql);
        Assert.Contains("observed_utc_date SET NOT NULL", migrationSql);
    }

    [Fact]
    public void DisneyParksMigration_SeedsOnlyOrlandoParks()
    {
        var migrationPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "Migrations",
            "004_seed_disney_parks.sql");
        var migrationSql = File.ReadAllText(Path.GetFullPath(migrationPath));
        var expectedSourceParkIds = new[] { 5, 6, 7, 8 };

        foreach (var sourceParkId in expectedSourceParkIds)
        {
            Assert.Contains($"({sourceParkId},", migrationSql);
        }

        Assert.DoesNotContain("(4,", migrationSql);
        Assert.DoesNotContain("(16,", migrationSql);
        Assert.Contains("ON CONFLICT (source_park_id) DO UPDATE", migrationSql);
        Assert.Contains("timezone = EXCLUDED.timezone", migrationSql);
    }

    [Fact]
    public void NonOrlandoCleanupMigration_RemovesDependentDataBeforeParks()
    {
        var migrationPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "Migrations",
            "005_remove_non_orlando_disney_parks.sql");
        var migrationSql = File.ReadAllText(Path.GetFullPath(migrationPath));

        var observationsDelete = migrationSql.IndexOf(
            "DELETE FROM public.queue_observations",
            StringComparison.Ordinal);
        var parksDelete = migrationSql.IndexOf(
            "DELETE FROM public.parks",
            StringComparison.Ordinal);

        Assert.True(observationsDelete >= 0);
        Assert.True(parksDelete > observationsDelete);
        Assert.Contains("source_park_id IN (4, 16, 17, 28, 30, 31, 274, 275)", migrationSql);
    }

    [Fact]
    public void AdminOperationsMigration_AddsSchedulingAndObservationAuditFields()
    {
        var migrationPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "Migrations",
            "006_add_admin_operations.sql");
        var migrationSql = File.ReadAllText(Path.GetFullPath(migrationPath));

        Assert.Contains("collection_enabled boolean", migrationSql);
        Assert.Contains("collection_interval_minutes integer", migrationSql);
        Assert.Contains("trigger_source text", migrationSql);
        Assert.Contains("is_valid boolean", migrationSql);
        Assert.Contains("invalid_reason text", migrationSql);
    }

    [Fact]
    public void AnalyticsReader_UsesWeekdayQuarterHourlyAggregates()
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
        Assert.Contains("observed_slot_minutes / 15", analyticsReaderSourceCode);
        Assert.Contains("AS LocalMinute", analyticsReaderSourceCode);
        Assert.Contains("observed_local_date AS LocalDate", analyticsReaderSourceCode);
        Assert.Contains("observed_local_time AS LocalTime", analyticsReaderSourceCode);
        Assert.Contains("observation.observed_at >= @FromInclusive", analyticsReaderSourceCode);
        Assert.Contains("observation.observed_at < @ToExclusive", analyticsReaderSourceCode);
        Assert.Contains("ClosedPercentage", analyticsReaderSourceCode);
        Assert.Contains("attraction_daily_waits", analyticsReaderSourceCode);
        Assert.Contains("AVG(daily.attraction_average_wait)", analyticsReaderSourceCode);
        Assert.Contains("observation.observed_local_date >= @FromInclusive", analyticsReaderSourceCode);
        Assert.Contains("WHERE park.is_active", analyticsReaderSourceCode);
    }

    [Fact]
    public void PredictionReader_UsesParkLocalHistoricalObservations()
    {
        var predictionReaderPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "PostgreSqlQueuePredictionReader.cs");
        var predictionReaderSourceCode =
            File.ReadAllText(Path.GetFullPath(predictionReaderPath));

        Assert.Contains("@TargetAt AT TIME ZONE park.timezone", predictionReaderSourceCode);
        Assert.Contains("observation.is_valid", predictionReaderSourceCode);
        Assert.Contains("observation.is_open", predictionReaderSourceCode);
        Assert.Contains("observation.wait_minutes IS NOT NULL", predictionReaderSourceCode);
        Assert.Contains("observation.observed_at >= @WindowStart", predictionReaderSourceCode);
        Assert.Contains("observation.observed_at < @WindowEnd", predictionReaderSourceCode);
        Assert.Contains("observation.observed_day_of_week", predictionReaderSourceCode);
        Assert.Contains("observation.observed_slot_minutes / 15", predictionReaderSourceCode);
    }

    [Fact]
    public void ItineraryReader_UsesCurrentAndParkLocalHistoricalWaits()
    {
        var readerPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "PostgreSqlItineraryCandidateReader.cs");
        var readerSourceCode = File.ReadAllText(Path.GetFullPath(readerPath));

        Assert.Contains("latest_observation.wait_minutes AS WaitMinutes", readerSourceCode);
        Assert.Contains("HistoricalWaitMinutes", readerSourceCode);
        Assert.Contains("COUNT(*) >= 3", readerSourceCode);
        Assert.Contains("percentile_cont(0.5)", readerSourceCode);
        Assert.Contains("@HistoricalTargetAt AT TIME ZONE park.timezone", readerSourceCode);
        Assert.Contains("observation.observed_day_of_week", readerSourceCode);
        Assert.Contains("observation.observed_slot_minutes / 15", readerSourceCode);
    }

    [Fact]
    public void WalkingTimeReader_RequiresActiveAttractionsInTheSamePark()
    {
        var readerPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "PostgreSqlWalkingTimeReader.cs");
        var readerSourceCode = File.ReadAllText(Path.GetFullPath(readerPath));

        Assert.Contains(
            "destination.park_id = origin.park_id",
            readerSourceCode);
        Assert.Contains("park.id = @ParkId", readerSourceCode);
        Assert.Contains("origin.is_active", readerSourceCode);
        Assert.Contains("destination.is_active", readerSourceCode);
        Assert.Contains("origin.latitude AS FromLatitude", readerSourceCode);
        Assert.Contains("destination.longitude AS ToLongitude", readerSourceCode);
        Assert.Contains("origin.route_node_id AS FromRouteNodeId", readerSourceCode);
        Assert.Contains("public.park_route_edges", readerSourceCode);
        Assert.Contains("edge.is_bidirectional", readerSourceCode);
    }

    [Fact]
    public void ParkRouteGraphMigration_ConstrainsNodesAndEdgesToOnePark()
    {
        var migrationPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "Migrations",
            "010_add_park_route_graph.sql");
        var migrationSql = File.ReadAllText(Path.GetFullPath(migrationPath));

        Assert.Contains("CREATE TABLE public.park_route_nodes", migrationSql);
        Assert.Contains("CREATE TABLE public.park_route_edges", migrationSql);
        Assert.Contains("FOREIGN KEY (from_node_id, park_id)", migrationSql);
        Assert.Contains("FOREIGN KEY (to_node_id, park_id)", migrationSql);
        Assert.Contains("CHECK (distance_meters > 0)", migrationSql);
        Assert.Contains("ADD COLUMN route_node_id bigint", migrationSql);
        Assert.Contains("FOREIGN KEY (route_node_id, park_id)", migrationSql);
    }

    [Fact]
    public void ItineraryCandidateReader_UsesLatestValidObservation()
    {
        var readerPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "PostgreSqlItineraryCandidateReader.cs");
        var readerSourceCode = File.ReadAllText(Path.GetFullPath(readerPath));

        Assert.Contains("LEFT JOIN LATERAL", readerSourceCode);
        Assert.Contains(
            "attraction.duration_minutes::integer AS DurationMinutes",
            readerSourceCode);
        Assert.Contains("observation.is_valid", readerSourceCode);
        Assert.Contains(
            "observation.observed_at >= @LiveObservationFrom",
            readerSourceCode);
        Assert.Contains(
            "observation.observed_at <= @LiveObservationTo",
            readerSourceCode);
        Assert.Contains("ORDER BY observation.observed_at DESC", readerSourceCode);
        Assert.Contains("LIMIT 1", readerSourceCode);
        Assert.Contains("attraction.id = ANY(@AttractionIds)", readerSourceCode);
        Assert.Contains("park.is_active", readerSourceCode);
    }

    [Fact]
    public void VisitSessionStore_ReplacesPendingPlanTransactionally()
    {
        var storePath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Disney.Infrastructure",
            "PostgreSqlVisitSessionStore.cs");
        var sourceCode = File.ReadAllText(Path.GetFullPath(storePath));

        Assert.Contains("FOR UPDATE", sourceCode);
        Assert.Contains("updated_at = @ExpectedUpdatedAt", sourceCode);
        Assert.Contains("DELETE FROM public.visit_session_stops", sourceCode);
        Assert.Contains("status = 'Pending'", sourceCode);
        Assert.Contains("total_queue_minutes = @TotalQueueMinutes", sourceCode);
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

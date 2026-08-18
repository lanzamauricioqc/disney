using System.Data.Common;
using System.Data;
using System.Globalization;
using System.Reflection;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Persistence.PostgreSql.Dapper;
using Repositories;

namespace Disney.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public void ServiceRegistration_RegistersEveryPersistenceService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=test;Username=test;Password=test"
            })
            .Build();
        var services = new ServiceCollection();

        var result = services.AddPostgreSqlDapperPersistence(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Same(services, result);
        Assert.IsType<NpgsqlConnectionFactory>(provider.GetRequiredService<IDbConnectionFactory>());
        Assert.IsType<DatabaseHealthCheck>(provider.GetRequiredService<IDatabaseHealthCheck>());
        Assert.IsType<ParksRepository>(scope.ServiceProvider.GetRequiredService<IParksRepository>());
        Assert.IsType<LandsRepository>(scope.ServiceProvider.GetRequiredService<ILandsRepository>());
        Assert.IsType<AttractionsRepository>(scope.ServiceProvider.GetRequiredService<IAttractionsRepository>());
        Assert.IsType<QueueObservationsRepository>(
            scope.ServiceProvider.GetRequiredService<IQueueObservationsRepository>());
        Assert.IsType<QueueCollectionRunsRepository>(
            scope.ServiceProvider.GetRequiredService<IQueueCollectionRunsRepository>());
    }

    [Fact]
    public void NpgsqlConnectionFactory_UsesDefaultConnectionString()
    {
        var configuration = BuildConfiguration("Default", "Host=default;Database=test");
        var factory = new NpgsqlConnectionFactory(configuration);

        var connection = Assert.IsType<NpgsqlConnection>(factory.CreateConnection());

        Assert.Contains("Host=default", connection.ConnectionString);
    }

    [Fact]
    public void NpgsqlConnectionFactory_FallsBackToDefaultConnection()
    {
        var configuration = BuildConfiguration("DefaultConnection", "Host=fallback;Database=test");
        var factory = new NpgsqlConnectionFactory(configuration);

        var connection = Assert.IsType<NpgsqlConnection>(factory.CreateConnection());

        Assert.Contains("Host=fallback", connection.ConnectionString);
    }

    [Fact]
    public void NpgsqlConnectionFactory_RequiresAConnectionString()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => new NpgsqlConnectionFactory(new ConfigurationBuilder().Build()));

        Assert.Contains("Connection string 'Default'", exception.Message);
    }

    [Fact]
    public async Task DatabaseHealthCheck_OpensConnectionAndExecutesQuery()
    {
        var healthCheck = new DatabaseHealthCheck(
            new DelegateConnectionFactory(() => new SqliteConnection("Data Source=:memory:")));

        await healthCheck.CheckAsync(CancellationToken.None);
    }

    [Fact]
    public void Repositories_ReadAndUpsertEntitiesAndCompleteRuns()
    {
        using var database = new SqliteRepositoryDatabase();
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        var parks = new ParksRepository(database);
        var lands = new LandsRepository(database);
        var attractions = new AttractionsRepository(database);
        var runs = new QueueCollectionRunsRepository(database);
        var observations = new QueueObservationsRepository(database);

        database.Execute("""
            INSERT INTO public.parks (id, source_park_id, name, timezone, created_at, updated_at)
            VALUES (1, 6, 'Magic Kingdom', 'UTC', @Now, @Now);
            """, new { Now = DateTimeOffset.UtcNow });

        var park = Assert.Single(parks.GetAll());
        Assert.Equal(6, park.SourceParkId);

        var land = lands.Upsert(new Land
        {
            ParkId = park.Id,
            SourceLandId = 10,
            Name = "Tomorrowland",
            IsActive = true
        });
        land.Name = "Tomorrowland Updated";
        land = lands.Upsert(land);

        Assert.Equal("Tomorrowland Updated", Assert.Single(lands.GetByParkId(park.Id)).Name);

        var attraction = attractions.Upsert(new Attraction
        {
            ParkId = park.Id,
            CurrentLandId = land.Id,
            SourceRideId = 20,
            Name = "Space Mountain",
            IsActive = true
        });
        attraction.Name = "Space Mountain Updated";
        attraction = attractions.Upsert(attraction);

        Assert.Equal(
            "Space Mountain Updated",
            Assert.Single(attractions.GetByParkId(park.Id)).Name);

        var run = runs.Start(park.Id, DateTimeOffset.UtcNow);
        runs.Complete(run.Id, DateTimeOffset.UtcNow, true);

        var observation = observations.Upsert(CreateObservation(run.Id, park.Id, land.Id, attraction.Id));
        var updatedObservation = CreateObservation(run.Id, park.Id, land.Id, attraction.Id);
        updatedObservation.WaitMinutes = 30;
        updatedObservation = observations.Upsert(updatedObservation);

        Assert.Equal(observation.Id, updatedObservation.Id);
        Assert.Equal(
            30L,
            database.QuerySingle<long>(
                "SELECT wait_minutes FROM public.queue_observations WHERE id = @Id",
                new { updatedObservation.Id }));
        Assert.Equal(
            1L,
            database.QuerySingle<long>(
                "SELECT success FROM public.queue_collection_runs WHERE id = @Id",
                new { run.Id }));
    }

    [Fact]
    public void RepositoryGuardsAndMissingRunCompletionThrow()
    {
        using var database = new SqliteRepositoryDatabase();

        Assert.Throws<ArgumentNullException>(() => new LandsRepository(database).Upsert(null!));
        Assert.Throws<ArgumentNullException>(() => new AttractionsRepository(database).Upsert(null!));
        Assert.Throws<ArgumentNullException>(() => new QueueObservationsRepository(database).Upsert(null!));
        Assert.Throws<InvalidOperationException>(
            () => new QueueCollectionRunsRepository(database)
                .Complete(404, DateTimeOffset.UtcNow, false, "missing"));
    }

    [Fact]
    public void DapperTypeHandlers_ParseAndSetSupportedValues()
    {
        DapperTypeHandlers.Register();

        var dateHandler = CreateHandler("DateOnlyHandler");
        Assert.Equal(new DateOnly(2026, 8, 18), InvokeParse<DateOnly>(dateHandler, new DateOnly(2026, 8, 18)));
        Assert.Equal(new DateOnly(2026, 8, 18), InvokeParse<DateOnly>(dateHandler, new DateTime(2026, 8, 18)));
        Assert.Equal(
            new DateOnly(2026, 8, 18),
            InvokeParse<DateOnly>(dateHandler, new DateTimeOffset(2026, 8, 18, 1, 2, 3, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 8, 18), InvokeParse<DateOnly>(dateHandler, "2026-08-18"));
        Assert.Throws<TargetInvocationException>(() => InvokeParse<DateOnly>(dateHandler, new object()));
        InvokeSetValue(dateHandler, new DateOnly(2026, 8, 18));

        var nullableDateHandler = CreateHandler("NullableDateOnlyHandler");
        Assert.Null(InvokeParse<DateOnly?>(nullableDateHandler, DBNull.Value));
        Assert.Equal(new DateOnly(2026, 8, 18), InvokeParse<DateOnly?>(nullableDateHandler, "2026-08-18"));
        InvokeSetValue(nullableDateHandler, new DateOnly(2026, 8, 18));
        InvokeSetValue<DateOnly?>(nullableDateHandler, null);

        var timeHandler = CreateHandler("TimeOnlyHandler");
        Assert.Equal(new TimeOnly(1, 2), InvokeParse<TimeOnly>(timeHandler, new TimeOnly(1, 2)));
        Assert.Equal(new TimeOnly(1, 2), InvokeParse<TimeOnly>(timeHandler, new TimeSpan(1, 2, 0)));
        Assert.Equal(new TimeOnly(1, 2), InvokeParse<TimeOnly>(timeHandler, new DateTime(2026, 8, 18, 1, 2, 0)));
        Assert.Equal(
            new TimeOnly(1, 2),
            InvokeParse<TimeOnly>(
                timeHandler,
                new DateTimeOffset(2026, 8, 18, 1, 2, 0, TimeSpan.Zero)));
        Assert.Equal(new TimeOnly(1, 2), InvokeParse<TimeOnly>(timeHandler, "01:02"));
        Assert.Throws<TargetInvocationException>(() => InvokeParse<TimeOnly>(timeHandler, new object()));
        InvokeSetValue(timeHandler, new TimeOnly(1, 2));

        var nullableTimeHandler = CreateHandler("NullableTimeOnlyHandler");
        Assert.Null(InvokeParse<TimeOnly?>(nullableTimeHandler, DBNull.Value));
        Assert.Equal(new TimeOnly(1, 2), InvokeParse<TimeOnly?>(nullableTimeHandler, "01:02"));
        InvokeSetValue(nullableTimeHandler, new TimeOnly(1, 2));
        InvokeSetValue<TimeOnly?>(nullableTimeHandler, null);
    }

    private static IConfiguration BuildConfiguration(string name, string value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{name}"] = value
            })
            .Build();

    private static QueueObservation CreateObservation(
        int runId,
        int parkId,
        int landId,
        int attractionId) =>
        new()
        {
            CollectionRunId = runId,
            ParkId = parkId,
            LandId = landId,
            AttractionId = attractionId,
            CollectedAt = DateTimeOffset.UtcNow,
            ObservedLocalDate = new DateOnly(2026, 8, 18),
            ObservedLocalTime = new TimeOnly(14, 0),
            ObservedLocalHour = 14,
            ObservedSlotMinutes = 0,
            ObservedDayOfWeek = 2,
            IsOpen = true,
            WaitMinutes = 20,
            SourceLastUpdated = DateTimeOffset.UtcNow,
            CreatedAt = default
        };

    private static object CreateHandler(string name) =>
        Activator.CreateInstance(
            typeof(DapperTypeHandlers).GetNestedType(name, BindingFlags.NonPublic)!)!;

    private static T InvokeParse<T>(object handler, object value) =>
        (T)handler.GetType().GetMethod("Parse")!.Invoke(handler, [value])!;

    private static void InvokeSetValue<T>(object handler, T value)
    {
        using var command = new SqliteCommand();
        var parameter = command.CreateParameter();
        handler.GetType().GetMethod("SetValue")!.Invoke(handler, [parameter, value]);
    }

    private sealed class DelegateConnectionFactory(Func<DbConnection> createConnection)
        : IDbConnectionFactory
    {
        public DbConnection CreateConnection() => createConnection();
    }

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value) =>
            value is DateTimeOffset dateTimeOffset
                ? dateTimeOffset
                : DateTimeOffset.Parse(
                    Convert.ToString(value, CultureInfo.InvariantCulture)!,
                    CultureInfo.InvariantCulture);

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) =>
            parameter.Value = value.ToString("O", CultureInfo.InvariantCulture);
    }

    private sealed class SqliteRepositoryDatabase : IDbConnectionFactory, IDisposable
    {
        private readonly string _databasePath =
            Path.Combine(Path.GetTempPath(), $"disney-tests-{Guid.NewGuid():N}.db");

        public SqliteRepositoryDatabase()
        {
            DapperTypeHandlers.Register();
            Execute("""
                CREATE TABLE public.parks (
                    id INTEGER PRIMARY KEY,
                    source_park_id INTEGER NOT NULL,
                    name TEXT NOT NULL,
                    timezone TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE public.lands (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    park_id INTEGER NOT NULL,
                    source_land_id INTEGER NOT NULL,
                    name TEXT NOT NULL,
                    is_active INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE public.attractions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    park_id INTEGER NOT NULL,
                    current_land_id INTEGER NULL,
                    source_ride_id INTEGER NOT NULL,
                    name TEXT NOT NULL,
                    is_active INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE public.queue_collection_runs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    park_id INTEGER NOT NULL,
                    started_at TEXT NULL,
                    completed_at TEXT NULL,
                    success INTEGER NOT NULL,
                    error_message TEXT NULL
                );
                CREATE TABLE public.queue_observations (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    collection_run_id INTEGER NOT NULL,
                    park_id INTEGER NOT NULL,
                    land_id INTEGER NULL,
                    attraction_id INTEGER NULL,
                    collected_at TEXT NOT NULL,
                    observed_local_date TEXT NOT NULL,
                    observed_local_time TEXT NOT NULL,
                    observed_local_hour INTEGER NOT NULL,
                    observed_slot_minutes INTEGER NOT NULL,
                    observed_day_of_week INTEGER NOT NULL,
                    is_open INTEGER NOT NULL,
                    wait_minutes INTEGER NULL,
                    source_last_updated TEXT NULL,
                    created_at TEXT NOT NULL,
                    UNIQUE (attraction_id, observed_local_date, observed_slot_minutes)
                );
                """);
        }

        public DbConnection CreateConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            connection.Execute($"ATTACH DATABASE '{_databasePath.Replace("'", "''")}' AS public;");
            return connection;
        }

        public void Execute(string sql, object? parameters = null)
        {
            using var connection = CreateConnection();
            connection.Execute(sql, parameters);
        }

        public T QuerySingle<T>(string sql, object? parameters = null)
        {
            using var connection = CreateConnection();
            return connection.QuerySingle<T>(sql, parameters);
        }

        public void Dispose()
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }
}

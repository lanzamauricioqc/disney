using Persistence.PostgreSql.Dapper;
using Repositories;
using WorkerModels;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId |
        ActivityTrackingOptions.SpanId |
        ActivityTrackingOptions.ParentId;
});
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

builder.Services.AddPostgreSqlDapperPersistence(builder.Configuration);

builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<IQueueCollectionJob, QueueCollectionJob>();
builder.Services.AddScoped<IQueueTimesCollector, QueueTimesCollector>();
builder.Services.AddSingleton<QueueObservationFactory>();
builder.Services.Configure<QueueCollectionOptions>(
    builder.Configuration.GetSection("QueueCollection"));

builder.Services.AddHttpClient<IQueueTimesProvider, QueueTimesClient>(client =>
{
    client.BaseAddress = new Uri("https://queue-times.com");
    client.Timeout = TimeSpan.FromSeconds(30);

    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "MagicKingdomQueueWorker/1.0");
});

var host = builder.Build();

{
    var logger = host.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("StartupDiagnostics");

    try
    {
        logger.LogInformation(
            new EventId(5000, "DatabaseConnectivityCheckStarted"),
            "Database connectivity check started.");
        var healthCheck = host.Services.GetRequiredService<IDatabaseHealthCheck>();
        await healthCheck.CheckAsync();
        logger.LogInformation(
            new EventId(5001, "DatabaseConnectivityCheckCompleted"),
            "Database connectivity check completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(
            new EventId(5002, "DatabaseConnectivityCheckFailed"),
            ex,
            "Database connectivity check failed.");
        throw;
    }
}

host.Run();

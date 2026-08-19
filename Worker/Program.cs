using Disney.Application;
using Disney.Infrastructure;
using Disney.Worker;

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

builder.Services.AddDisneyInfrastructure(builder.Configuration);

builder.Services.AddHostedService<QueueCollectionWorker>();
builder.Services.AddScoped<IQueueCollectionService, QueueCollectionService>();
builder.Services.AddScoped<IQueueCollectionJob, QueueCollectionJob>();
builder.Services.Configure<QueueCollectionOptions>(
    builder.Configuration.GetSection("QueueCollection"));

var host = builder.Build();

{
    var logger = host.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("StartupDiagnostics");

    try
    {
        var migrator = host.Services.GetRequiredService<IDatabaseMigrator>();
        await migrator.MigrateAsync();
        logger.LogInformation(
            new EventId(5000, "DatabaseConnectivityCheckStarted"),
            "Database connectivity check started.");
        var healthCheck = host.Services.GetRequiredService<IDatabaseHealthCheck>();
        await healthCheck.CheckAsync();
        logger.LogInformation(
            new EventId(5001, "DatabaseConnectivityCheckCompleted"),
            "Database connectivity check completed successfully.");
    }
    catch (Exception exception)
    {
        logger.LogError(
            new EventId(5002, "DatabaseConnectivityCheckFailed"),
            exception,
            "Database connectivity check failed.");
        throw;
    }
}

host.Run();

using Persistence.PostgreSql.Dapper;
using Repositories;
using WorkerModels;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPostgreSqlDapperPersistence(builder.Configuration);

builder.Services.AddHostedService<Worker>();

builder.Services.AddHttpClient<QueueTimesClient>(client =>
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
        logger.LogInformation("Attempting to open database connection...");
        var healthCheck = host.Services.GetRequiredService<IDatabaseHealthCheck>();
        await healthCheck.CheckAsync();
        logger.LogInformation("Database connectivity test succeeded.");
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Database connectivity test failed. Ensure the configured database is available and the connection string is correct.");
    }
}

host.Run();

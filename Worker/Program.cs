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
builder.Services
    .AddOptions<QueueCollectionOptions>()
    .Bind(builder.Configuration.GetSection(QueueCollectionOptions.SectionName))
    .Validate(
        options => options.Interval > TimeSpan.Zero,
        "QueueCollection:Interval must be greater than zero.")
    .ValidateOnStart();
builder.Services
    .AddOptions<DatabaseStartupOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseStartupOptions.SectionName))
    .Validate(
        options => options.MaxAttempts > 0,
        "DatabaseStartup:MaxAttempts must be greater than zero.")
    .Validate(
        options => options.InitialDelay > TimeSpan.Zero,
        "DatabaseStartup:InitialDelay must be greater than zero.")
    .Validate(
        options => options.MaxDelay >= options.InitialDelay,
        "DatabaseStartup:MaxDelay must be greater than or equal to InitialDelay.")
    .ValidateOnStart();
builder.Services.AddSingleton<DatabaseStartupInitializer>();

var host = builder.Build();

await host.Services.GetRequiredService<DatabaseStartupInitializer>()
    .InitializeAsync();

host.Run();

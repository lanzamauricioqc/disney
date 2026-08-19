using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Disney.Api;
using Disney.Application;
using Disney.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("current-waits", policy => policy.Expire(TimeSpan.FromSeconds(30)));
    options.AddPolicy("analytics", policy => policy.Expire(TimeSpan.FromMinutes(10)));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddDisneyInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IQueueAnalyticsService, QueueAnalyticsService>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var application = builder.Build();

application.UseExceptionHandler();
application.UseRateLimiter();
application.UseCors();
application.UseOutputCache();
application.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Disney Queue Analytics API v1");
    options.RoutePrefix = "swagger";
});

application.MapOpenApi();
application.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();
application.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
application.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
application.MapQueueAnalyticsEndpoints();

await application.Services.GetRequiredService<IDatabaseMigrator>().MigrateAsync();
application.Run();

public partial class Program;

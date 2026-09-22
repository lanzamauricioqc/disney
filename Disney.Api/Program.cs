using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Disney.Api;
using Disney.Application;
using Disney.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy(
        "parks",
        policy => policy.Expire(TimeSpan.FromMinutes(30)).Tag("parks"));
    options.AddPolicy(
        "current-waits",
        policy => policy.Expire(TimeSpan.FromSeconds(30)).Tag("current-waits"));
    options.AddPolicy(
        "analytics",
        policy => policy.Expire(TimeSpan.FromMinutes(4)).Tag("analytics"));
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
    options.AddPolicy("company-auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("company-integration", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Request.Headers["X-Api-Key"].ToString() is { Length: > 0 } key
                ? key[..Math.Min(key.Length, 12)]
                : context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
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
builder.Services.Configure<CompanyJwtOptions>(
    builder.Configuration.GetSection(CompanyJwtOptions.SectionName));
var companyJwtOptions = builder.Configuration
    .GetSection(CompanyJwtOptions.SectionName)
    .Get<CompanyJwtOptions>() ?? new CompanyJwtOptions();
var validationSigningKey = companyJwtOptions.IsValid
    ? Encoding.UTF8.GetBytes(companyJwtOptions.SigningKey)
    : RandomNumberGenerator.GetBytes(64);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = companyJwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = companyJwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(validationSigningKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "email",
            RoleClaimType = "role",
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CompanyMember", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(CompanyClaimNames.OrganizationId);
        policy.RequireClaim("sub");
        policy.RequireClaim("email");
        policy.RequireClaim("role");
    })
    .AddPolicy("CompanyTeamManagement", policy =>
        policy.RequireRole("Owner", "Administrator"))
    .AddPolicy("CompanyBillingManagement", policy =>
        policy.RequireRole("Owner", "Administrator"))
    .AddPolicy("CompanyConfigurationManagement", policy =>
        policy.RequireRole("Owner", "Administrator"));
builder.Services.AddSingleton<ICompanyTokenService, CompanyJwtTokenService>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<IQueueAnalyticsService, QueueAnalyticsService>();
builder.Services.AddScoped<IQueuePredictionService, QueuePredictionService>();
builder.Services.AddScoped<IWalkingTimeService, WalkingTimeService>();
builder.Services.AddScoped<IItineraryOptimizationService, ItineraryOptimizationService>();
builder.Services.AddScoped<IVisitSessionService, VisitSessionService>();
builder.Services.AddScoped<IQueueCollectionService, QueueCollectionService>();
builder.Services.AddScoped<WaitlistService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services
    .AddOptions<QueueTimesHealthCheckOptions>()
    .Bind(builder.Configuration.GetSection(QueueTimesHealthCheckOptions.SectionName))
    .Validate(
        options => options.SourceParkId > 0,
        "HealthChecks:QueueTimes:SourceParkId must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<QueueTimesHealthCheck>(
        "queue-times",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["queue-times"],
        timeout: TimeSpan.FromSeconds(5));

var application = builder.Build();

application.UseExceptionHandler();
application.UseRateLimiter();
application.UseCors();
application.UseOutputCache();
application.UseAuthentication();
application.UseAuthorization();
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
application.MapHealthChecks("/health/dependencies/queue-times", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("queue-times")
});
application.MapParkEndpoints();
application.MapQueueAnalyticsEndpoints();
application.MapQueuePredictionEndpoints();
application.MapWalkingTimeEndpoints();
application.MapItineraryOptimizationEndpoints();
application.MapVisitSessionEndpoints();
application.MapAdminEndpoints();
application.MapCommercialEndpoints();
application.MapCompanyEndpoints();

await application.Services.GetRequiredService<IDatabaseMigrator>().MigrateAsync();
application.Run();

public partial class Program;

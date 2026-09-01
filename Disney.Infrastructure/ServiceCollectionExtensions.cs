using Disney.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Disney.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDisneyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        DapperTypeHandlers.Register();
        services.AddSingleton(new PostgreSqlConnectionFactory(configuration));
        services.AddSingleton<IDatabaseMigrator, PostgreSqlMigrator>();
        services.AddSingleton<IDatabaseHealthCheck, PostgreSqlDatabaseHealthCheck>();
        services.AddScoped<IParkReader, PostgreSqlParkReader>();
        services.AddScoped<IQueueCollectionStore, PostgreSqlQueueCollectionStore>();
        services.AddScoped<IQueueAnalyticsReader, PostgreSqlQueueAnalyticsReader>();
        services.AddScoped<IAdminRepository, PostgreSqlAdminRepository>();
        services.AddScoped<IWaitlistRepository, PostgreSqlCommercialRepository>();
        services.AddScoped<ICommercialRepository, PostgreSqlCommercialRepository>();
        services.AddScoped<IPaymentCheckoutGateway, StripePaymentCheckoutGateway>();
        services.AddScoped<IPaymentWebhookHandler, StripePaymentWebhookHandler>();
        services.AddScoped<ICompanyRepository, PostgreSqlCompanyRepository>();
        services.AddSingleton<ICompanyPasswordService, CompanyPasswordService>();
        services.AddSingleton<ICompanySecretService, CompanySecretService>();
        services.AddScoped<ICompanyNotificationOutbox, PostgreSqlCompanyNotificationOutbox>();
        services.AddScoped<ICompanyBillingGateway, StripeCompanyBillingGateway>();
        services.Configure<StripeOptions>(
            configuration.GetSection(StripeOptions.SectionName));
        services.AddSingleton<QueueObservationFactory>();

        services.AddHttpClient<IQueueTimesProvider, QueueTimesClient>(httpClient =>
        {
            httpClient.BaseAddress = new Uri("https://queue-times.com");
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DisneyQueueWorker/2.0");
        });

        return services;
    }
}

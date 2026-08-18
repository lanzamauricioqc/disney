using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repositories;

namespace Persistence.PostgreSql.Dapper;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgreSqlDapperPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        DapperTypeHandlers.Register();

        services.AddSingleton<IDbConnectionFactory>(_ => new NpgsqlConnectionFactory(configuration));
        services.AddSingleton<IDatabaseHealthCheck, DatabaseHealthCheck>();
        services.AddScoped<IParksRepository, ParksRepository>();
        services.AddScoped<ILandsRepository, LandsRepository>();
        services.AddScoped<IAttractionsRepository, AttractionsRepository>();
        services.AddScoped<IQueueObservationsRepository, QueueObservationsRepository>();
        services.AddScoped<IQueueCollectionRunsRepository, QueueCollectionRunsRepository>();

        return services;
    }
}

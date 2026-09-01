using Disney.Application;
using Microsoft.Extensions.Options;

namespace Disney.Worker;

internal sealed class DatabaseStartupInitializer(
    IDatabaseMigrator migrator,
    IDatabaseHealthCheck healthCheck,
    IOptions<DatabaseStartupOptions> options,
    ILogger<DatabaseStartupInitializer> logger)
{
    private readonly DatabaseStartupOptions _options = options.Value;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var retryDelay = _options.InitialDelay;

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                await migrator.MigrateAsync(cancellationToken);
                await healthCheck.CheckAsync(cancellationToken);
                logger.LogInformation(
                    "Database startup checks completed successfully on attempt {Attempt}.",
                    attempt);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (attempt < _options.MaxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Database startup checks failed on attempt {Attempt}. Retrying in {RetryDelay}.",
                    attempt,
                    retryDelay);
                await Task.Delay(retryDelay, cancellationToken);
                retryDelay = GetNextDelay(retryDelay, _options.MaxDelay);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Database startup checks failed after {AttemptCount} attempts.",
                    _options.MaxAttempts);
                throw;
            }
        }
    }

    private static TimeSpan GetNextDelay(TimeSpan currentDelay, TimeSpan maximumDelay)
    {
        if (currentDelay >= maximumDelay / 2)
        {
            return maximumDelay;
        }

        return currentDelay + currentDelay;
    }
}

namespace Repositories;

public interface IDatabaseHealthCheck
{
    Task CheckAsync(CancellationToken cancellationToken = default);
}

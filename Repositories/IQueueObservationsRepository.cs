namespace Repositories;

public interface IQueueObservationsRepository
{
    QueueObservation Upsert(QueueObservation entity);
}

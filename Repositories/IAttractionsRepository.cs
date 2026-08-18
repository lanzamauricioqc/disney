namespace Repositories;

public interface IAttractionsRepository
{
    IReadOnlyList<Attraction> GetByParkId(int parkId);

    Attraction Upsert(Attraction entity);
}

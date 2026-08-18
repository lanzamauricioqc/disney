namespace Repositories;

public interface ILandsRepository
{
    IReadOnlyList<Land> GetByParkId(int parkId);

    Land Upsert(Land entity);
}

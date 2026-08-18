namespace Repositories;

public interface IParksRepository
{
    IReadOnlyList<Park> GetAll();
}

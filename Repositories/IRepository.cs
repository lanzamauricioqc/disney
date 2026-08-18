namespace Repositories;

public interface IRepository<TEntity, in TKey>
{
    IEnumerable<TEntity> GetAll();

    TEntity? GetById(TKey id);

    TEntity InsertOrUpdate(TEntity entity);

    bool DeleteById(TKey id);
}

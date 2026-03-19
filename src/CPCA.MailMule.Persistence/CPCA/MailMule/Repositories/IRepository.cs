namespace CPCA.MailMule.Repositories;

using System.Linq.Expressions;

public interface IRepository<T> where T : class
{
    IQueryable<T> AsQueryable();
    
    Task<T?> GetByIdAsync(Int64 id, CancellationToken cancellationToken = default);
    
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, Boolean>> predicate, CancellationToken cancellationToken = default);
    
    Task<List<T>> ToListAsync(Expression<Func<T, Boolean>>? predicate = null, CancellationToken cancellationToken = default);
    
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    
    void Update(T entity);
    
    void UpdateRange(IEnumerable<T> entities);
    
    void Remove(T entity);
    
    void RemoveRange(IEnumerable<T> entities);
    
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

namespace CPCA.MailMule.Repositories;

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

public class EfCoreRepository<T> : IRepository<T> where T : class
{
    private readonly MailMuleDbContext dbContext;
    private readonly DbSet<T> dbSet;

    public EfCoreRepository(MailMuleDbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.dbSet = dbContext.Set<T>();
    }

    public IQueryable<T> AsQueryable() => this.dbSet.AsQueryable();

    public async Task<T?> GetByIdAsync(Int64 id, CancellationToken cancellationToken = default)
    {
        return await this.dbSet.FindAsync(new Object[] { id }, cancellationToken: cancellationToken);
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, Boolean>> predicate, CancellationToken cancellationToken = default)
    {
        return await this.dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<List<T>> ToListAsync(Expression<Func<T, Boolean>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var query = this.dbSet.AsQueryable();
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await this.dbSet.AddAsync(entity, cancellationToken);
        await this.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await this.dbSet.AddRangeAsync(entities, cancellationToken);
        await this.SaveChangesAsync(cancellationToken);
    }

    public void Update(T entity)
    {
        this.dbSet.Update(entity);
    }

    public void UpdateRange(IEnumerable<T> entities)
    {
        this.dbSet.UpdateRange(entities);
    }

    public void Remove(T entity)
    {
        this.dbSet.Remove(entity);
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        this.dbSet.RemoveRange(entities);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await this.dbContext.SaveChangesAsync(cancellationToken);
    }
}

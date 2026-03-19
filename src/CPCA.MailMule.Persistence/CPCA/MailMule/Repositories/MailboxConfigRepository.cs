namespace CPCA.MailMule.Repositories;

using Microsoft.EntityFrameworkCore;

public sealed class MailboxConfigRepository : EfCoreRepository<MailboxConfig>
{
    public MailboxConfigRepository(MailMuleDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<List<MailboxConfig>> GetByMailboxTypeAsync(String mailboxType, CancellationToken cancellationToken = default)
    {
        return await this.AsQueryable()
            .Where(m => m.MailboxType.ToString() == mailboxType)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<Boolean> ExistsByDisplayNameAsync(String displayName, CancellationToken cancellationToken = default)
    {
        return await this.AsQueryable()
            .AnyAsync(m => m.DisplayName == displayName, cancellationToken);
    }
}

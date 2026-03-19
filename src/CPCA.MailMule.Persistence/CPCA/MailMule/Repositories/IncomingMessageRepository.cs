namespace CPCA.MailMule.Repositories;

using Microsoft.EntityFrameworkCore;

public sealed class IncomingMessageRepository : EfCoreRepository<IncomingMessage>
{
    public IncomingMessageRepository(MailMuleDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<List<IncomingMessage>> GetByMailboxConfigIdAsync(Int64 mailboxConfigId, CancellationToken cancellationToken = default)
    {
        return await this.AsQueryable()
            .Where(x => x.MailboxConfigId == mailboxConfigId)
            .OrderBy(x => x.Uid)
            .ToListAsync(cancellationToken);
    }

    public async Task<IncomingMessage?> GetByMailboxAndUidAsync(Int64 mailboxConfigId, UInt32 uid, CancellationToken cancellationToken = default)
    {
        return await this.AsQueryable()
            .FirstOrDefaultAsync(x => x.MailboxConfigId == mailboxConfigId && x.Uid == uid, cancellationToken);
    }
}
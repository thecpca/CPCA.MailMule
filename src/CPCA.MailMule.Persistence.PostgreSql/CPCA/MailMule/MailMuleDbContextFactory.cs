using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CPCA.MailMule;

public sealed class MailMuleDbContextFactory : IDesignTimeDbContextFactory<MailMuleDbContext>
{
    public MailMuleDbContext CreateDbContext(String[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MAILMULE_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=MailMule;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<MailMuleDbContext>();
        optionsBuilder.UsePostgreSql(connectionString);

        return new MailMuleDbContext(optionsBuilder.Options);
    }
}

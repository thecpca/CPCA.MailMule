using Microsoft.EntityFrameworkCore;

namespace CPCA.MailMule;

public static class DbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder UsePostgreSql(this DbContextOptionsBuilder builder, String connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.UseNpgsql(
            connectionString,
            options => options.MigrationsAssembly(typeof(CpcaMailMulePersistencePostgreSql).Assembly.FullName));
    }
}

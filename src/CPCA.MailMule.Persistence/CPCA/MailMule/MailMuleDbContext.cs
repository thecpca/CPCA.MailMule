using Microsoft.EntityFrameworkCore;

namespace CPCA.MailMule;

public sealed class MailMuleDbContext(DbContextOptions<MailMuleDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationSettings> ApplicationSettings => Set<ApplicationSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationSettings>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.InactivityTimeoutMinutes)
                .IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}

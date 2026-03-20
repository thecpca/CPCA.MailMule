using Microsoft.EntityFrameworkCore;

namespace CPCA.MailMule;

public sealed class MailMuleDbContext(DbContextOptions<MailMuleDbContext> options) : DbContext(options)
{
    public DbSet<MailboxConfig> MailboxConfigs => Set<MailboxConfig>();

    public DbSet<IncomingMessage> IncomingMessages => Set<IncomingMessage>();

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    public DbSet<ApplicationSettings> ApplicationSettings => Set<ApplicationSettings>();

    public DbSet<ActiveSession> ActiveSessions => Set<ActiveSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailboxConfig>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.ImapHost)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.ImapPort)
                .IsRequired();

            entity.Property(x => x.Username)
                .HasMaxLength(320)
                .IsRequired();

            entity.Property(x => x.EncryptedPassword)
                .HasMaxLength(4000)
                .IsRequired();

            entity.Property(x => x.InboxFolderPath)
                .HasMaxLength(255);

            entity.Property(x => x.OutboxFolderPath)
                .HasMaxLength(255);

            entity.Property(x => x.SentFolderPath)
                .HasMaxLength(255);

            entity.Property(x => x.ArchiveFolderPath)
                .HasMaxLength(255);

            entity.Property(x => x.JunkFolderPath)
                .HasMaxLength(255);

            entity.Property(x => x.PollIntervalSeconds)
                .IsRequired();

            entity.Property(x => x.DeleteMessage)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.Property(x => x.MailboxType)
                .HasConversion<Int32>()
                .IsRequired();

            entity.Property(x => x.Security)
                .HasConversion<Int32>()
                .IsRequired();

            entity.HasIndex(x => new { x.MailboxType, x.SortOrder });
        });

        modelBuilder.Entity<UserSettings>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.UndoWindowSeconds)
                .IsRequired();

            entity.Property(x => x.PageSize)
                .IsRequired();

            entity.HasData(new UserSettings
            {
                Id = 1,
                UndoWindowSeconds = 15,
                PageSize = 25
            });
        });

        modelBuilder.Entity<ApplicationSettings>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.InactivityTimeoutMinutes)
                .IsRequired();

            entity.HasData(new ApplicationSettings
            {
                Id = 1,
                InactivityTimeoutMinutes = 30
            });
        });

        modelBuilder.Entity<IncomingMessage>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.MailboxConfigId)
                .IsRequired();

            entity.Property(x => x.Uid)
                .IsRequired();

            entity.Property(x => x.UidValidity)
                .IsRequired();

            entity.Property(x => x.State)
                .HasConversion<Int32>()
                .IsRequired();

            entity.Property(x => x.DiscoveredUtc)
                .IsRequired();

            entity.Property(x => x.LastSeenUtc)
                .IsRequired();

            entity.Property(x => x.ErrorCode)
                .HasMaxLength(100);

            entity.Property(x => x.ErrorDetail)
                .HasMaxLength(1000);

            entity.HasIndex(x => new { x.MailboxConfigId, x.Uid })
                .IsUnique();

            entity.HasIndex(x => new { x.MailboxConfigId, x.State });
        });

        modelBuilder.Entity<ActiveSession>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .ValueGeneratedOnAdd();

            entity.Property(x => x.Kingdom)
                .HasConversion<Int32>()
                .IsRequired();

            entity.Property(x => x.UserId)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.UserName)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.SessionStartedUtc)
                .IsRequired();

            entity.Property(x => x.LastActivityUtc)
                .IsRequired();

            entity.HasIndex(x => x.Kingdom)
                .IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}

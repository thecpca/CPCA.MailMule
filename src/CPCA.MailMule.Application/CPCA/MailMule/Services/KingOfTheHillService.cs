namespace CPCA.MailMule.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

internal sealed class KingOfTheHillService : IKingOfTheHillService
{
    private readonly MailMuleDbContext dbContext;
    private readonly ILogger<KingOfTheHillService> logger;

    public KingOfTheHillService(MailMuleDbContext dbContext, ILogger<KingOfTheHillService> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SessionStatusDto> GetSessionStatusAsync(Kingdom kingdom, CancellationToken cancellationToken = default)
    {
        var appSettings = await this.dbContext.ApplicationSettings.FirstAsync(cancellationToken);
        var activeSession = await this.dbContext.ActiveSessions
            .FirstOrDefaultAsync(s => s.Kingdom == kingdom, cancellationToken);

        if (activeSession is null)
        {
            return new SessionStatusDto(
                IsCurrentUserKing: false,
                CurrentKingUserId: null,
                CurrentKingUserName: null,
                SessionStartedUtc: DateTimeOffset.MinValue,
                LastActivityUtc: DateTimeOffset.MinValue,
                InactivityTimeoutMinutes: appSettings.InactivityTimeoutMinutes,
                Kingdom: kingdom);
        }

        return new SessionStatusDto(
            IsCurrentUserKing: false,
            CurrentKingUserId: activeSession.UserId,
            CurrentKingUserName: activeSession.UserName,
            SessionStartedUtc: activeSession.SessionStartedUtc,
            LastActivityUtc: activeSession.LastActivityUtc,
            InactivityTimeoutMinutes: appSettings.InactivityTimeoutMinutes,
            Kingdom: kingdom);
    }

    public async Task<SessionClaimResult> ClaimKingshipAsync(Kingdom kingdom, String userId, String userName, CancellationToken cancellationToken = default)
    {
        var appSettings = await this.dbContext.ApplicationSettings.FirstAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var timeout = TimeSpan.FromMinutes(appSettings.InactivityTimeoutMinutes);

        var activeSession = await this.dbContext.ActiveSessions
            .FirstOrDefaultAsync(s => s.Kingdom == kingdom, cancellationToken);

        if (activeSession is not null)
        {
            if (String.Equals(activeSession.UserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                activeSession.LastActivityUtc = now;
                await this.dbContext.SaveChangesAsync(cancellationToken);

                this.logger.LogInformation(
                    "User {UserId} ({UserName}) refreshed their king of the hill session for {Kingdom}",
                    userId, userName, kingdom);

                return new SessionClaimResult(
                    Success: true,
                    CurrentKingUserId: activeSession.UserId,
                    CurrentKingUserName: activeSession.UserName,
                    FailureReason: null);
            }

            var inactivityDuration = now - activeSession.LastActivityUtc;

            if (inactivityDuration >= timeout)
            {
                this.logger.LogInformation(
                    "Previous king {PreviousUserId} ({PreviousUserName}) was deposed from {Kingdom} due to inactivity after {InactivityMinutes} minutes. New king: {UserId} ({UserName})",
                    activeSession.UserId,
                    activeSession.UserName,
                    kingdom,
                    inactivityDuration.TotalMinutes,
                    userId,
                    userName);

                activeSession.UserId = userId;
                activeSession.UserName = userName;
                activeSession.SessionStartedUtc = now;
                activeSession.LastActivityUtc = now;
                await this.dbContext.SaveChangesAsync(cancellationToken);

                return new SessionClaimResult(
                    Success: true,
                    CurrentKingUserId: userId,
                    CurrentKingUserName: userName,
                    FailureReason: null);
            }

            this.logger.LogWarning(
                "User {UserId} ({UserName}) was denied king of the hill for {Kingdom}. Current king: {CurrentUserId} ({CurrentUserName}), last activity: {LastActivity} ({InactivityMinutes} minutes ago)",
                userId,
                userName,
                kingdom,
                activeSession.UserId,
                activeSession.UserName,
                activeSession.LastActivityUtc,
                inactivityDuration.TotalMinutes);

            return new SessionClaimResult(
                Success: false,
                CurrentKingUserId: activeSession.UserId,
                CurrentKingUserName: activeSession.UserName,
                FailureReason: SessionClaimFailureReason.AnotherUserIsKing);
        }

        this.dbContext.ActiveSessions.Add(new ActiveSession
        {
            Kingdom = kingdom,
            UserId = userId,
            UserName = userName,
            SessionStartedUtc = now,
            LastActivityUtc = now
        });

        await this.dbContext.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation(
            "User {UserId} ({UserName}) claimed king of the hill for {Kingdom}",
            userId, userName, kingdom);

        return new SessionClaimResult(
            Success: true,
            CurrentKingUserId: userId,
            CurrentKingUserName: userName,
            FailureReason: null);
    }

    public async Task RecordActivityAsync(Kingdom kingdom, CancellationToken cancellationToken = default)
    {
        var activeSession = await this.dbContext.ActiveSessions
            .FirstOrDefaultAsync(s => s.Kingdom == kingdom, cancellationToken);

        if (activeSession is not null)
        {
            activeSession.LastActivityUtc = DateTimeOffset.UtcNow;
            await this.dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ReleaseKingshipAsync(Kingdom kingdom, CancellationToken cancellationToken = default)
    {
        var activeSession = await this.dbContext.ActiveSessions
            .FirstOrDefaultAsync(s => s.Kingdom == kingdom, cancellationToken);

        if (activeSession is not null)
        {
            this.logger.LogInformation(
                "User {UserId} ({UserName}) released king of the hill for {Kingdom}",
                activeSession.UserId, activeSession.UserName, kingdom);

            this.dbContext.ActiveSessions.Remove(activeSession);
            await this.dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace CPCA.MailMule.Services;

/// <summary>
/// Manages "King of the Hill" exclusivity per <see cref="Kingdom"/>.
/// Each kingdom tracks its own king and inactivity timer independently.
/// </summary>
public interface IKingOfTheHillService
{
    /// <summary>
    /// Returns the current session status for the specified kingdom.
    /// </summary>
    Task<SessionStatusDto> GetSessionStatusAsync(Kingdom kingdom, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to claim kingship of the specified kingdom for the given user.
    /// If the kingdom is unclaimed or the current king has exceeded the inactivity timeout,
    /// the claim succeeds and the caller becomes king. Otherwise the claim is denied.
    /// </summary>
    Task<SessionClaimResult> ClaimKingshipAsync(Kingdom kingdom, String userId, String userName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records heartbeat activity for the specified kingdom, resetting the inactivity timer.
    /// </summary>
    Task RecordActivityAsync(Kingdom kingdom, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases kingship of the specified kingdom.
    /// </summary>
    Task ReleaseKingshipAsync(Kingdom kingdom, CancellationToken cancellationToken = default);
}

public record SessionStatusDto(
    Boolean IsCurrentUserKing,
    String? CurrentKingUserId,
    String? CurrentKingUserName,
    DateTimeOffset SessionStartedUtc,
    DateTimeOffset LastActivityUtc,
    Int32 InactivityTimeoutMinutes,
    Kingdom Kingdom
);

public record SessionClaimResult(
    Boolean Success,
    String? CurrentKingUserId,
    String? CurrentKingUserName,
    SessionClaimFailureReason? FailureReason
);

public enum SessionClaimFailureReason
{
    AnotherUserIsKing,
    InactivityTimeoutExceeded
}

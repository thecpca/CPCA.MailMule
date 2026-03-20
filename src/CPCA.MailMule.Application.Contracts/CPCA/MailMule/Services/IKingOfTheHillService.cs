namespace CPCA.MailMule.Services;

public interface IKingOfTheHillService
{
    Task<SessionStatusDto> GetSessionStatusAsync(CancellationToken cancellationToken = default);

    Task<SessionClaimResult> ClaimKingshipAsync(String userId, String userName, CancellationToken cancellationToken = default);

    Task RecordActivityAsync(CancellationToken cancellationToken = default);

    Task ReleaseKingshipAsync(CancellationToken cancellationToken = default);
}

public record SessionStatusDto(
    Boolean IsCurrentUserKing,
    String? CurrentKingUserId,
    String? CurrentKingUserName,
    DateTimeOffset SessionStartedUtc,
    DateTimeOffset LastActivityUtc,
    Int32 InactivityTimeoutMinutes
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

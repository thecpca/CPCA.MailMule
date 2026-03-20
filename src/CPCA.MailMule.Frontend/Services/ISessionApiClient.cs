namespace CPCA.MailMule.Frontend.Services;

public interface ISessionApiClient
{
    Task<SessionStatusDto?> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<SessionClaimResultDto?> ClaimAsync(CancellationToken cancellationToken = default);
    Task<Boolean> HeartbeatAsync(CancellationToken cancellationToken = default);
    Task<Boolean> ReleaseAsync(CancellationToken cancellationToken = default);
}

public record SessionStatusDto(
    Boolean IsCurrentUserKing,
    String? CurrentKingUserId,
    String? CurrentKingUserName,
    DateTimeOffset SessionStartedUtc,
    DateTimeOffset LastActivityUtc,
    Int32 InactivityTimeoutMinutes
);

public record SessionClaimResultDto(
    Boolean Success,
    String? CurrentKingUserId,
    String? CurrentKingUserName,
    SessionClaimFailureReasonDto? FailureReason
);

public enum SessionClaimFailureReasonDto
{
    AnotherUserIsKing,
    InactivityTimeoutExceeded
}

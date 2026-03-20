namespace CPCA.MailMule.Frontend.Services;

public interface ISessionApiClient
{
    Task<SessionStatusDto?> GetStatusAsync(Kingdom kingdom, CancellationToken cancellationToken = default);
    Task<SessionClaimResultDto?> ClaimAsync(Kingdom kingdom, CancellationToken cancellationToken = default);
    Task<Boolean> HeartbeatAsync(Kingdom kingdom, CancellationToken cancellationToken = default);
    Task<Boolean> ReleaseAsync(Kingdom kingdom);
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

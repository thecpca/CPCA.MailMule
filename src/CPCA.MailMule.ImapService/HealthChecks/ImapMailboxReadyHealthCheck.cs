using CPCA.MailMule.Repositories;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CPCA.MailMule.ImapService.HealthChecks;

internal sealed class ImapMailboxReadyHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<MailboxConfigRepository>();
        var stringProtector = scope.ServiceProvider.GetRequiredService<IStringProtector>();

        var incoming = await repository.GetByMailboxTypeAsync("Incoming", cancellationToken);
        var outgoing = await repository.GetByMailboxTypeAsync("Outgoing", cancellationToken);

        var activeMailboxes = incoming
            .Concat(outgoing)
            .Where(x => x.IsActive)
            .DistinctBy(x => x.Id)
            .ToList();

        if (activeMailboxes.Count == 0)
        {
            return HealthCheckResult.Healthy("No active mailboxes are configured.");
        }

        var failed = new List<string>();

        foreach (var mailbox in activeMailboxes)
        {
            try
            {
                using var client = new ImapClient();
                var password = stringProtector.Unprotect(mailbox.EncryptedPassword);

                await client.ConnectAsync(
                    mailbox.ImapHost,
                    mailbox.ImapPort,
                    ToSecureSocketOptions(mailbox.Security),
                    cancellationToken);

                await client.AuthenticateAsync(mailbox.Username, password, cancellationToken);
                await client.NoOpAsync(cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);
            }
            catch (Exception ex)
            {
                failed.Add($"{mailbox.Id}:{ex.Message}");
            }
        }

        if (failed.Count > 0)
        {
            return HealthCheckResult.Unhealthy(
                $"{failed.Count} mailbox connectivity check(s) failed.",
                data: new Dictionary<string, object>
                {
                    ["failedMailboxes"] = failed
                });
        }

        return HealthCheckResult.Healthy($"Validated {activeMailboxes.Count} active mailbox connection(s).");
    }

    private static SecureSocketOptions ToSecureSocketOptions(MailboxSecurity security)
    {
        return security switch
        {
            MailboxSecurity.Ssl => SecureSocketOptions.SslOnConnect,
            MailboxSecurity.Tls => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.Auto,
        };
    }
}

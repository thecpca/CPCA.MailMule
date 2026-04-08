using CPCA.MailMule.Dtos;

namespace CPCA.MailMule.Backend.Controllers;

internal static class MailboxChangeTracking
{
    internal static IReadOnlyList<String> GetChangedFields(MailboxConfigDto existingMailbox, UpdateMailboxConfigDto updatedMailbox)
    {
        var changedFields = new List<String>();

        if (!String.Equals(existingMailbox.DisplayName, updatedMailbox.DisplayName, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.DisplayName));
        if (!String.Equals(existingMailbox.ImapHost, updatedMailbox.ImapHost, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.ImapHost));
        if (existingMailbox.ImapPort != updatedMailbox.ImapPort) changedFields.Add(nameof(existingMailbox.ImapPort));
        if (!String.Equals(existingMailbox.MailboxType, updatedMailbox.MailboxType, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.MailboxType));
        if (!String.Equals(existingMailbox.Security, updatedMailbox.Security, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.Security));
        if (!String.Equals(existingMailbox.Username, updatedMailbox.Username, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.Username));
        if (!String.Equals(existingMailbox.InboxFolderPath, updatedMailbox.InboxFolderPath, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.InboxFolderPath));
        if (!String.Equals(existingMailbox.OutboxFolderPath, updatedMailbox.OutboxFolderPath, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.OutboxFolderPath));
        if (!String.Equals(existingMailbox.SentFolderPath, updatedMailbox.SentFolderPath, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.SentFolderPath));
        if (!String.Equals(existingMailbox.ArchiveFolderPath, updatedMailbox.ArchiveFolderPath, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.ArchiveFolderPath));
        if (!String.Equals(existingMailbox.JunkFolderPath, updatedMailbox.JunkFolderPath, StringComparison.Ordinal)) changedFields.Add(nameof(existingMailbox.JunkFolderPath));
        if (existingMailbox.PollIntervalSeconds != updatedMailbox.PollIntervalSeconds) changedFields.Add(nameof(existingMailbox.PollIntervalSeconds));
        if (existingMailbox.DeleteMessage != updatedMailbox.DeleteMessage) changedFields.Add(nameof(existingMailbox.DeleteMessage));
        if (existingMailbox.IsActive != updatedMailbox.IsActive) changedFields.Add(nameof(existingMailbox.IsActive));
        if (existingMailbox.SortOrder != updatedMailbox.SortOrder) changedFields.Add(nameof(existingMailbox.SortOrder));
        if (!String.IsNullOrWhiteSpace(updatedMailbox.Password)) changedFields.Add(nameof(updatedMailbox.Password));

        return changedFields;
    }
}

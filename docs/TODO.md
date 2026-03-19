# vNext (Out of Scope for v2)

In no particular order:

## Outgoing Mailbox Organization

- Destination search and filtering in the operator UI.
- Grouping destinations by department/region.

## Convenience Features

- Keyboard shortcuts for routing
- Auto-suggest routing based on:
  - Keywords
  - Sender Domain
  - Previous routes
- Spam Scoring with auto-route thresholds
- When configuring IMAP Mailboxes (Incoming or Outgoing) retrieve the list of folders in the mailbox and use the list to let the user choose Inbox, Junk, Archive, and Trash

## Appearance

- Light/Dark/System Modes
- Branding (Marvin the Mail Mule, CPCA)

## Multi-User Support

- Multi-operator support with per-message locking.
  - Will require SignalR integration for real-time collaboration
  - When a message is selected by one user, it is automatically "locked", appearing disabled/dimmed for all other users.
  - Messages remain locked until they are routed, cancelled or no longer selected.

## Code Quality

- Use `[LoggerMessage]` attributes to improve logging consistency and performance
- Use TwoRivers Rhuarc.EntityFrameworkCore for applying EF Core migrations at runtime
- Use Mapperly for mapping Entities to DTO's
- Switch to FluentAssertions for Unit and Integration testing
- Switch to TwoRivers Rhuarc.Testing integration testing framework

## Application Insights

- Message Counters
  - Received (Per incoming mailbox)
  - Delivered (Per outgoing mailbox)
  - Quarantined (Spam)
  - Errors
  - Processed by User/Operator

# 🐴 MailMule — Implementation Plan (v2)

> *"He hauls your mail so you do not have to."*

## Overview 

An organization has a small number of mailboxes that serve as the primary point of contact for various departments. For example, info@thecpca.ca, payments@thecpca.ca, complaints@thecpca.ca. Each incoming message needs a response from an appropriate staff members. However giving staff members access to the incoming mailboxes creates the opportunity for messages to be responded to more than once, which is undesirable from both the original sender's perception, and duplication of staff effort. This means that the message must be routed to an appropriate person who can then reply as needed. However, the current mail system only allows a message to be forwarded, which has an undesirable side effect: Replying to the forwarded message, by default, sends the reply to the mailbox from which the email was forwarded. This means that when the staff member replies they must remember to update the "To" to the original sender. This has proven to be unreliable, since the staff member thinks that they replied, but the original sender receives nothing.

**MailMule** is a simple application that solves the above problems by moving messages instead of forwarding them. It relies on accessing both the incoming and outgoing mailboxes with IMAP. By simply moving the message from one IMAP folder to another, the headers are not changed, and the message just shows up in the staff member's Inbox as if they had been the original recipient. When the staff member replies, it automatically goes to the original sender. No additional cognitive load on the Staff member, and seamless continuity with the original sender.

MailMule is configured with multiple incoming IMAP accounts and multiple outgoing IMAP accounts. Sources and Destinations. The application displays four columns. The leftmost column is the "Inbox" which aggregates and displays the headers of the messages from all incoming mailboxes. When the user selects a message header from the list, the full message is retrieved and displayed in the second column, much as it would be in any standard email program. The third column is a list of message routing targets, the first of which is a button that marks the message as spam/junk. The rest are the available "outgoing" IMAP accounts. The forth column is the "Outbox" queue. 

When the user clicks one of the routing options, the currently selected message moves from the "Inbox" column, to the "Outbox" column. This immediately starts an "Undo" timer for that message. The next message from the Inbox is automatically selected and displayed.

The Undo Timer gives the user the chance to change their mind. If they hit the Cancel button for the message, the timer is stopped and the message is moved back to the Inbox. They may alternatively click an "Execute" button which stops the timer and immediately sends a command back to the server to perform the routing action. If the user does not click Cancel or Execute, the timer will elapse and the message will be routed as specified.

If the routing action was "Mark as Spam", then the message is moved to the incoming mailbox's junk folder. If the routing action is Deliver, the the message is moved to the recipients default folder, probably their Inbox.

IMPORTANT: No operations are executed against the IMAP folders by the server until the timer expires or the user short-circuits the timer by clicking Execute.

---

## Core Principles (Non-Negotiable)

### 🔒 Privacy

- No email content or metadata (subject, sender, recipient, body, headers) is stored in any database.
- Only persisted per-message fields: mailbox identity, UID, UIDVALIDITY, state, and timestamps.
- All message headers and bodies are fetched live from the incoming IMAP mailbox on demand.
- Logs (SEQ) may temporarily contain sender, recipient, subject, and date; retention is capped at 30 days.

### 🕳️ Destination Mailboxes Are Write-Only Black Holes

- The only permitted operation against a destination mailbox is IMAP `APPEND`.
- MailMule must never list, search, fetch, flag, or inspect any message in a destination mailbox.
- Success is determined by a successful `APPEND` response from the server, nothing more.

### 📬 Routing Is a Move, Not a Copy

- When a message is routed, it is `APPEND`ed to the destination mailbox and then physically removed from the incoming mailbox.
- No retained copy remains anywhere in the incoming IMAP account after successful routing.
- Cross-account IMAP `MOVE` does not exist; the operation is always `APPEND` to destination + `DELETE/EXPUNGE` from incoming.

### 🗑️ Junk Handling Stays Within Incoming

- Marking a message as junk moves it to a configured junk/spam folder **within** the incoming IMAP account.
- MailMule does not delete messages; it only relocates them to junk folders or the destination mailboxes.

### Delete vs Archive

- A message that has been routed to an outgoing mailbox must be removed from it's incoming folder. 
- Incoming IMAP mailbox `DeleteMessage`:
  - `true`: the message is deleted/expunged from the IMAP Inbox folder
  - `false`: the message is moved to the IMAP Archive folder

### 📺 The UI Is Always a Live Projection

- The operator inbox reflects the **current contents** of the incoming folder at all times.
- If a message disappears from incoming externally, it simply disappears from the UI. No tracking of why.
- The background worker continuously synchronizes incoming state into in-process projection.
- Message state is NOT persisted across sessions. If the front end application is closed when messages have been queued for routing and the Undo Timers have not elapsed, nothing is saved. The next time the application is opened and the user logs in, the Inbox will show the aggregated incoming messages at that point in time and the outbox will be empty.

### 👤 King of the Hill — Per-Kingdom Session Exclusivity

Certain workflows require single-operator exclusivity. Rather than locking the entire application, exclusivity is scoped to **kingdoms** — logical groups of pages that share a single "King of the Hill" lock.

#### Kingdoms

- A `Kingdom` enum (`CPCA.MailMule.Domain.Shared`) defines the available groups.
- Initial kingdoms: `MessageRouting` (1) and `ErrorQueue` (2).
- A page belongs to at most one kingdom. Not all pages require exclusivity.
- A user MAY hold kingship in multiple kingdoms simultaneously.
- Inactivity timers are tracked **per kingdom**, not per user.

#### Attribute-Based Declaration

- Pages that require exclusivity are decorated with `[KingOfTheHill(Kingdom.X)]` (defined in `CPCA.MailMule.Application.Contracts`).
- Pages without the attribute are unrestricted — no session check occurs.
- The attribute targets classes only (`[AttributeUsage(AttributeTargets.Class)]`), is not inheritable, and does not allow multiples.

#### Backend Enforcement

- The backend maintains an `ActiveSession` row per kingdom in the database (unique index on `Kingdom`).
- Session endpoints are kingdom-scoped: `/session/{kingdom}/status`, `/session/{kingdom}/claim`, `/session/{kingdom}/heartbeat`, `/session/{kingdom}/release`.
- When a user claims a kingdom, the backend checks for an existing session:
  - If no session exists, the user becomes king.
  - If the current king has been inactive longer than `ApplicationSettings.InactivityTimeoutMinutes`, the new user usurps the session.
  - If the current king is still active, the claim is denied and the response includes the current king's identity.
- Heartbeat calls update `LastActivityUtc` for the specific kingdom.

#### Frontend SessionMonitor

- `SessionMonitor.razor` is integrated at the `MainLayout` level and receives the current page type via a cascading `RouteData` parameter.
- On each navigation, `SessionMonitor` uses reflection to check whether the routed page type carries a `[KingOfTheHillAttribute]`.
  - If present: claims the kingdom, starts a heartbeat timer, and shows a blocking overlay if the user is not king.
  - If absent: tears down any active session for the previous kingdom and renders the page without restriction.
- When the user navigates away from a kingdom-protected page, `SessionMonitor` releases the session for that kingdom.

#### Inactivity Timeout

- Stored in `ApplicationSettings.InactivityTimeoutMinutes` (default: 30).
- Configured via the `/admin/app-settings` page (Admin role only).

### ⏱️ Routing Has a 15-Second Undo Window

- Clicking a route button does not execute immediately.
- A visible countdown banner gives the operator 15 seconds to cancel the routing operation. We call it an Undo Timer.
- After 15 seconds, the routing action executes automatically.
- Each message has it's own undo timer.
- The undo delay is operator-configurable in the admin UI (valid range: 5–60 seconds).

### 🔐 Authentication Is SSO-Ready

- Auth is implemented behind a thin `IUserContext` abstraction.
- v2 uses Authentik OIDC at `https://auth.dkw.io` in the BFF.
- Backend provides cookie authentication/session to Frontend.
- Backend issues signed JWT tokens to ImapService and proxies `api/*` to ImapService.
- Authorization (claims/role policy enforcement) is in scope and must be implemented.
- Future OIDC/SAML provider changes should require only auth wiring changes in `Program.cs` and `IUserContext` integration.

### Architecture is BFF + Microservice

- CPCA.MailMule.Frontend: Blazor WebAssembly Standalone SPA using MudBlazor for controls and theming.
- CPCA.MailMule.Backend: ASP.NET BFF handling Authentication and Authorization, issuing internal JWT, and proxying API traffic to ImapService.
- CPCA.MailMule.ImapService: Manages IMAP connections and performs message routing operations.
- SignalR support is optional in current scope (primarily for new-message notifications).

### Concrete Decision Rule

- If it depends on OIDC, cookies, roles, browser requests, or UI/admin policy, put it in Backend.
- If it depends on IMAP, message UIDs, UIDVALIDITY, folder sync, route/junk execution, or mailbox health, put it in ImapService.
- If it is purely session UX state and should vanish when the browser closes, keep it in Frontend.

---

## v2 Technology Stack

| Concern       | Choice                                     |
|---------------|--------------------------------------------|
| Framework     | .NET 10, ASP.NET Core                      |
| Orchestration | Aspire 13.x                                |
| UI            | Blazor WebAssembly Standalone + MudBlazor  |
| IMAP          | MailKit                                    |
| Database      | EF Core (PostgreSQL via Npgsql)            |
| Logging       | Serilog → SEQ                              |
| Secrets       | Database as Encrypted values               |
| Testing       | xUnit + Moq; integration tests             |

---

## Solution Structure

```
MailMule.slnx
├── src/
│   ├── CPCA.MailMule.Application            — Application services and IMAP workflow implementations
│   ├── CPCA.MailMule.Application.Contracts  — Contracts/interfaces/DTOs shared across boundaries
│   ├── CPCA.MailMule.Core                   — Shared cross-cutting primitives and abstractions
│   ├── CPCA.MailMule.Domain                 — Domain models
│   ├── CPCA.MailMule.Domain.Shared          — Value objects and enums
│   ├── CPCA.MailMule.Persistence            — EF Core DbContext and persistence implementation
│   ├── CPCA.MailMule.Persistence.PostgreSql — Npgsql DbContext factory, PostgreSQL migrations, provider wiring
│   ├── CPCA.MailMule.Backend                — BFF auth, authorization, proxy and configuration API
│   ├── CPCA.MailMule.ImapService            — IMAP microservice and routing operations
│   ├── CPCA.MailMule.Frontend               — Blazor WebAssembly app (operator + admin UI)
│   └── CPCA.MailMule.Shared                 — Shared endpoint/resource constants
└── tests/
    ├── CPCA.MailMule.Application.IntegrationTests
    ├── CPCA.MailMule.Domain.UnitTests
    ├── CPCA.MailMule.Persistence.UnitTests                    — EF Core In Memory
    └── CPCA.MailMule.Persistence.PostgreSql.IntegrationTests  — PostgreSQL in a Container
```

### Planned Cleanup

- Remove `CPCA.MailMule.WebApi` and `CPCA.MailMule.WebApi.Client` from the solution.
- Remove SQLite-specific projects (`CPCA.MailMule.Persistence.Sqlite`, `CPCA.MailMule.Sqlite`).
- Keep PostgreSQL as the only database provider for v2.

## ✅ Phase 0 — Constraints Document

### Goal

Produce a machine-readable constraints reference and record any SmarterMail-specific IMAP behavior that must
be accounted for during implementation.

### Deliverable

`docs/IMAP-Router-Constraints.md` documenting:

- The full privacy constraints.
- The write-only destination rule.
- Routing-as-move semantics (APPEND + DELETE, no retained copy).
- Junk-within-incoming semantics.
- SmarterMail-specific notes: folder naming conventions (e.g., `Junk E-mail`), UIDPLUS availability, MOVE
  extension availability, IDLE extension availability.
- The polling-only decision for v2.

---

## ✅ Phase 1 — Solution Skeleton

### Goal

Scaffold the solution and all projects so that the build is green before any implementation begins.

### Deliverable

- `MailMule.slnx` with projects `docs/`, `src/`, and `tests/`
- Correct `<ProjectReference>` entries per the rules above.
- `CPCA.MailMule.ImapService` hosts IMAP workflow services and API endpoints for routing operations.
- `CPCA.MailMule.Backend` hosts BFF authentication, authorization, and API proxying.
- `CPCA.MailMule.Frontend` uses `WebAssemblyHostBuilder` with MudBlazor configured.
- Empty DI registrations for all interfaces defined in Phase 2 onward (stubs only, so the build stays green
  throughout implementation).
- MudBlazor NuGet package added to `CPCA.MailMule.Frontend`.
- Solution builds cleanly with no warnings.

---

## [~] Phase 2 — Configuration & Admin Backbone

### Goal

Define the full configuration model for mailboxes, destinations, and operational settings, then build the
admin UI. Configuration is implemented before the IMAP layer so that IMAP services can be driven by real data
from day one.

Current status:
- Mailbox configuration persistence, CRUD APIs, and `/admin/incoming` + `/admin/outgoing` pages are implemented.
- Connection testing is implemented.
- `ApplicationSettings` and `UserSettings` persistence, services, DTOs, API endpoints are implemented.
- `/admin/settings` (Operator/Admin) contains UserSettings (UndoWindowSeconds, PageSize).
- `/admin/app-settings` (Admin only) contains ApplicationSettings (InactivityTimeoutMinutes).
- **Folder discovery is not yet implemented** — administrators must manually enter folder paths.

### Data Model (persisted through EF Core)

**`MailboxConfig`**
- `Id` (GUID)
- `MailboxType` (Enum: Undefined, Incoming, Outgoing)
- `DisplayName` (string)
- `ImapHost` (string)
- `ImapPort` (int)
- `Security` (Enum: Undefined, Ssl, Tls, Auto)
- `Username` (string)
- `EncryptedPassword` (string) — Data Protection-protected password payload
- `InboxFolderPath` (string) — e.g., `INBOX`
- `JunkFolderPath` (string) — e.g., `Junk E-mail` (required for Incoming mailboxes)
- `ArchiveFolderPath` (string) — e.g., `Archives` (required for Incoming mailboxes when DeleteMessage = false)
- `ErrorFolderPath` (string) — e.g., `Error`
- `PollIntervalSeconds` (int) — default 20
- `DeleteMessage` (boolean) — default false (Archive)
- `IsActive` (bool)
- `LastPolledUtc` (DateTimeOffset)

> **Note on Folder Names**: IMAP folder naming conventions vary by server. "INBOX" is the standard default folder, but some servers (e.g., SmarterMail) may use different conventions. When configuring a mailbox, administrators should verify or select the correct folder path.

**`UserSettings`** (single-row table)
- `UndoWindowSeconds` (int) — default 15; valid range 5–60
- `PageSize` (int) — Applies to UI Only. Default is 25

**`ApplicationSettings`** (single-row table)
- `InactivityTimeoutMinutes` (int) — default 30

**`ActiveSession`** (one row per kingdom, unique index on `Kingdom`)
- `Id` (int) — auto-generated identity
- `Kingdom` (Kingdom enum, stored as int) — which exclusivity group this session governs
- `UserId` (string, max 255) — the OIDC subject identifier of the current king
- `UserName` (string, max 255) — display name of the current king (shown to denied users)
- `SessionStartedUtc` (DateTimeOffset) — when the user first claimed this kingdom
- `LastActivityUtc` (DateTimeOffset) — updated on every heartbeat; used for inactivity timeout comparison

### Secret Handling

- Passwords are entered in the admin UI and stored encrypted at rest in the database.
- Encryption and decryption use ASP.NET Core Data Protection.
- Data Protection key ring files are persisted to a shared folder mounted for Backend and ImapService.
- Key ring protection is a mounted `.pfx` certificate and password from an environment variable.
- A `IStringProtector` interface abstracts encryption/decryption without leaking Data Protection details to callers.

### Admin UI (MudBlazor, protected by `Admin` role)

- `/admin/incoming` — list/add/edit/remove `MailboxConfig.MailboxType == Incoming` records.
  - **Folder Discovery**: After entering credentials, admin can click "Discover Folders" to retrieve a list of top-level folders from the IMAP server.
  - This confirms INBOX existence or allows selection of the correct default folder.
  - **Required folders for Incoming mailboxes**:
    - Inbox folder (selectable from discovered folders, defaults to `INBOX`)
    - Archive folder (selectable from discovered folders, required when `DeleteMessage = false`)
    - Junk folder (selectable from discovered folders)
  - Connection-test button.
- `/admin/outgoing` — list/add/edit/remove `MailboxConfig.MailboxType == Outgoing` records; drag-to-reorder `SortOrder`.
  - **Folder discovery required**: Admin must select the default (destination) folder from discovered folders. INBOX is suggested as default if it exists, but any top-level folder can be selected.
- `/admin/settings` — edit `UserSettings`.

### Folder Discovery (Phase 2.5)

#### Goal

Allow administrators to discover available folders on an IMAP server before saving mailbox configuration. This ensures correct folder paths and is required for configuring Archive and Junk folders on incoming mailboxes.

#### Implementation

1. **Backend** (`IImapConnectionTester` or new `IFolderDiscoveryService`):
   - Add method to retrieve top-level folders from IMAP server
   - Reuse existing IMAP connection logic from `IImapConnectionTester`

2. **Backend API** (`/admin/incoming/test-connection` → extend or new endpoint):
   - Add optional parameter to include folder list in connection test response
   - Or create new `/admin/discover-folders` endpoint

3. **Frontend** (`MailboxConfigForm`):
   - Add "Discover Folders" button (appears after successful connection test)
   - Display list of folders in a selectable dropdown
   - Auto-populate folder fields when selected

4. **Validation**:
   - Incoming mailboxes: require Inbox, Archive (if DeleteMessage=false), and Junk folder selection
   - Outgoing mailboxes: require selection of default (destination) folder from discovered folders

---

## [~] Phase 3 — IMAP Abstraction Layer

### Goal

Encapsulate all IMAP operations so that safety rules are enforced by the type system, not by convention.

Current status:
- `IMailboxService`, `IImapConnectionTester`, and `IStringProtector` are implemented.
- Core MailKit routing and retrieval operations exist with structured logging.
- `IImapClientFactory` and `MailKitImapClientFactory` are implemented; `MailKitMailboxService` refactored to use factory.
- `IOutgoingMailboxService` and pooled/persisted IMAP client abstractions are not implemented.

### Interfaces (in `CPCA.MailMule.Application.Contracts`)

```csharp

public readonly record struct MailboxId(Guid Value);

public readonly record struct MessageId(MailboxId, UInt32 UID);

// Ephemeral — never persisted
public record MessageHeader(MessageId Id, DateTimeOffset Date, FullEmail From, String Subject);

public interface IMailboxService
{
    Task<IReadOnlyList<MessageHeader>> GetHeadersAsync(CancellationToken cancellationToken = default);
    Task<MimeMessage> GetMessageAsync(MessageId messageId, CancellationToken cancellationToken = default);
    Task RouteToJunkAsync(MessageId messageId, CancellationToken cancellationToken = default);
    Task RouteToMailboxAsync(MessageId messageId, MailboxId mailboxId, CancellationToken cancellationToken = default);
}

public interface IImapClientFactory
{
    Task<ImapClient> CreateClientAsync(MailboxConfig mailbox, CancellationToken cancellationToken = default);
}

public interface IStringProtector
{
  String Protect(String plainText);
  String Unprotect(String cipherText);
}

public interface IFolderDiscoveryService
{
    Task<IReadOnlyList<String>> GetTopLevelFoldersAsync(MailboxConnectionTestRequest request, CancellationToken cancellationToken = default);
}
```

> **Folder Discovery**: The `IFolderDiscoveryService` interface allows administrators to retrieve a list of top-level folders from an IMAP server before saving mailbox configuration. This helps confirm the correct folder names (e.g., `INBOX` vs `INBOX/Test`) and allows selection of Archive and Junk folders for incoming mailboxes.

### Implementation Rules

- `ImapClient` should be pooled/persisted
- All operations are async and accept `CancellationToken`.
- Structured log entries are emitted at start and completion of every IMAP operation (operation name, UID,
  duration — never message content).
- `IOutgoingMailboxService` exposes no method that could retrieve or list messages. This is structural, not
  a comment.
- Incoming removal behavior follows `MailboxConfig.DeleteMessage`:
  - `true`: mark `\Deleted` and `EXPUNGE`
  - `false`: move to configured archive folder
- If archive folder does not exist and `DeleteMessage = false`, prompt to create it before routing begins.

---

## [~] Phase 4 — ImapService

### Goal

Manage IMAP connections, handle commands and queries for IMAP Mailboxes.

Current status:
- `CPCA.MailMule.ImapService` exists and exposes authenticated message query and routing endpoints.
- Backend-to-ImapService JWT authentication is wired.
- The operational error-state model described below is not implemented as a service-side workflow.

### Post Office

- No `RoutingAction` database table is used in v2.
- Operational auditing is provided by structured logs in SEQ only.
- Error queue state is represented from current operational state (in-process and mailbox projection), not persisted email metadata.

> **Hard rule**: No `Subject`, `From`, `To`, `Body`, `ReceivedDate`, or any email content/metadata columns in relational storage.

### State Transitions

```
New       → Routing   Operator clicks a destination button (undo window starts)
Routing   → New       Operator cancels within undo window
Routing   → Routed    APPEND + (EXPUNGE|MOVE) from incoming both succeeded
Routing   → Error     APPEND succeeded but (EXPUNGE|MOVE) from incoming failed (partial failure)
New       → Junk      Operator clicks Mark as Junk; IMAP MOVE succeeded
New       → Error     Any unrecoverable IMAP failure
Error     → New       Admin re-queues the message for retry
```

---

## [~] Phase 5 — Incoming Sync Worker

### Goal

Keep the "PostOffice" in-memory projection in sync with the real-time contents of the incoming IMAP folders.

Current status:
- The Frontend has an in-memory `PostOffice` and periodically refreshes message headers from the API.
- There is no hosted background worker in Backend or ImapService implementing the mailbox sync algorithm below.
- `UIDVALIDITY` handling and `IncomingMessage` operational state are not implemented.

### Algorithm

0. For each Active Incoming Mailbox
1. Connect to the incoming IMAP mailbox.
2. Fetch all UIDs and the current `UIDVALIDITY` value.
3. **If `UIDVALIDITY` has changed**: delete all `MessageHeader` records for this mailbox, then re-import all current UIDs as `State = New` (the mailbox was recreated or renamed; prior UID assignments are invalid).
4. **New UIDs**: for each UID present in IMAP but absent from the PostOffice, insert `IncomingMessage(State = New)`.
5. **Disappeared UIDs**: for each UID in the PostOffice with `State = New` or `Error` that is no longer present in IMAP, remove from PostOffice. No reason is recorded.
6. UIDs with `State = Routing`, `Routed`, or `Junk` are unaffected by the disappearance check.
7. Log the cycle result (count added, count removed) using Serilog structured logging. No message content logged.
8. Wait `PollIntervalSeconds`, then repeat.

### Notes

- The worker must handle IMAP connection failures gracefully: log the error and retry on the next cycle.

---

## [~] Phase 6 — Routing and Junk Workflows

### Goal

Implement the two key operator-triggered workflows with strict, deterministic failure handling.

Current status:
- Junk and route actions are implemented end-to-end through the Frontend queue and ImapService endpoints.
- Undo timing exists in the Frontend `PostOffice`.
- The explicit persisted/in-memory state machine (`New`, `Routing`, `Routed`, `Error`, `Junk`) and partial-failure requeue handling are not implemented as described below.

### Routing Workflow (`RouteMessageAsync`)

```
Pre-check: IncomingMessage.State must be Routing. If not, no-op (idempotency guard).

1. Fetch full MimeMessage from incoming IMAP by MessageId (IMailboxService.GetMessageAsync).
2. Route to destination mailbox (IMailboxService.RouteToMailboxAsync), internally performing APPEND.
   → On failure: State → Error, log to SEQ, return.
3. REMOVE the message from incoming (Move to Archive folder, or Delete/Expunge based on `DeleteMessage`).
   → On failure: State → Error, log to SEQ, return.
     NOTE: Do NOT retry APPEND. The message now exists in the destination; double-append would duplicate it.
           The operator must investigate using the error queue.
4. State → Routed.
5. Log sender, subject, date, destination name to SEQ only (not persisted to relational storage).
```

**Partial failure handling**: If Step 2 succeeds but Step 3 fails, the message exists in the destination
mailbox AND remains in incoming. `State = Error`. The admin error queue surfaces this. The admin can requeue
(which re-runs from Step 3 only, skipping the APPEND since the destination already has the message) or
dismiss (accepts the duplicate and removes from incoming manually).

### Junk Workflow (`MarkAsJunkAsync`)

```
Pre-check: IncomingMessage.State must be New. If not, no-op.

1. MOVE the message from incoming folder to junk folder (IMailboxService.RouteToJunkAsync).
   → On failure: State → Error, log to SEQ, return.
2. State → Junk.
3. Log sender, subject, date to SEQ only.
```

### Undo Window

The undo window is managed in the **web application layer**, not the routing service:

1. Operator clicks "Route to X":
   - `IncomingMessage.State` → `Routing`.
   - A `PendingRoutingQueue` singleton schedules the execution after `UndoWindowSeconds`.
   - The UI shows a countdown banner.
2. Operator clicks **Undo** before the timer expires:
   - Timer cancelled.
   - `IncomingMessage.State` → `New`.
   - No IMAP operations have occurred.
3. Timer fires:
   - `RouteMessageAsync` is invoked.
   - UI banner clears on success or shows error on failure.

---

## [~] Phase 7 — Operator Web UI

### Goal

A clean, fast email-client-like interface. The operator should feel at home immediately.

Current status:
- The message router page, mailbox actions, HTML sanitization, and incoming/outgoing admin pages exist.
- The UI is functional but does not fully match the target layout and scope described below.
- `/admin/settings` is still missing.

### Layout

- **Shell**: `MudLayout` + `MudAppBar` (MailMule title + user name + sign-out). No drawer needed for the
  operator view.
- **Message Router page** (`/Message/Router`):
  - Left pane: Vertical `MudStack` of `MudPaper` blocks showing incoming messages. Data = `MessageHeader` fetched live from IMAP. 
    Top Line: Subject. Second Line: {From} `MudSpacer` {Date}. Clicking a row loads the preview.
  - Center pane: when a message is selected, fetch the full body via `GetMessageAsync`. HTML bodies are
    sanitized before rendering as `MarkupString` (use `Ganss.Xss.HtmlSanitizer`). Plain-text bodies render
    in a `<pre>` block.
  - Routing List (Vertical `MudStack` beside preview): one `MudButton` "Mark as Junk". one `MudButton` per active destination (ordered by `SortOrder`). All buttons enabled when an "Inbox" message is selected; otherwise disabled.
  - Undo Queue (Vertical `MudStack` to the right of Routing List): 
    - Top Line: Subject.
    - Second Line: {From} `MudSpacer` {Date}.
    - Third Line: "Routing to `Destination` in X seconds… [[Cancel]] [[Execute Now]]". Countdown updates every second.
- **Admin pages** (`/admin/*`): see Phase 2.

### Accessibility and Safety

- HTML email bodies must always pass through the sanitizer before rendering. Never use raw `MarkupString`
  without sanitization.
- Show `MudProgressCircular` while IMAP fetches are in flight.
- Show `MudAlert Severity="Error"` (non-dismissible) on IMAP or routing failure.

---

## [~] Phase 8 — Authentication & Authorization

### Goal

Authenticate the user when a page is marked with the [[Authorize]] attribute.

Current status:
- OIDC sign-in, BFF session cookies, `/bff/user`, and Backend-issued JWTs for ImapService are implemented.
- Pages and endpoints require authentication.
- Role/policy enforcement for distinct `Operator` and `Admin` access is not fully implemented.

### Design

- Backend authenticates via OIDC (Authentik) and issues cookie sessions to Frontend.
- Backend authorizes access using claims/roles policy enforcement.
- Roles: `Operator` (access to `/`) and `Admin` (access to `/admin/`).

### SSO Seam

Replacing the auth provider in future requires:

1. A new `IUserContext` implementation wired to the OIDC/SAML claims principal.
2. A change to `Program.cs` auth configuration.
3. Zero changes to routing services, admin pages, or the persistence layer.

---

## [~] Phase 9 — Reliability, Observability & Testing

### Health Checks

- ImapService: `/health/live` — process is alive.
- ImapService: `/health/ready` — IMAP Mailboxes are connected, verified with `NOOP`
- Backend: `/health/ready` — EF Core database is reachable
- Implemented via `Microsoft.Extensions.Diagnostics.HealthChecks`.

### Structured Logging (Serilog → SEQ)

Configured in `MailMule.ImapService`, `MailMule.Backend`, and `MailMule.Frontend`. Every log event carries a `CorrelationId`.

| Event                  | Properties logged (SEQ only)              | Properties persisted (PostgreSQL) |
|------------------------|-------------------------------------------|-----------------------------------|
| Poll cycle             | Added count, removed count, errors        | None                              |
| Routing attempt        | Sender, subject, date, destination name   | None                              |
| Junk action            | Sender, subject, date                     | None                              |
| Admin config change    | Field name, user (no passwords)           | None                              |

### Error Queue

- `/admin/errors` lists all `IncomingMessage` records with `State = Error`.
- Each row shows: UID, error timestamp, and current error detail from operational state.
- **Re-queue**: resets `State = New` so the worker and operator can retry.
- **Dismiss**: admin accepts the situation and removes the item from the active error queue.

### Integration Tests

See: [END_TO_END_TEST_PLAN.md](END_TO_END_TEST_PLAN.md)

Current status:
- Backend database and ImapService mailbox readiness checks are implemented along with `/health/live` and `/health/ready` endpoints.
- Correlation ID middleware and propagation are implemented in Backend and ImapService.
- Structured logs for routing, junk, and admin mailbox changes are implemented.
- Web test coverage exists for the health endpoints and correlation headers.
- Error queue (`/admin/errors`) is implemented with list, requeue, and dismiss operations (backend API + frontend page).
- Poll-cycle worker logging and broader integration coverage are still outstanding.

---

## [ ] Phase 10 — Deployment

- Docker Compose on cpcad01 (Ubuntu 24.04 LTS)
  - Authentik:latest
  - PostgreSql:latest
  - ImapService
  - Backend
  - Frontend
- Local bind mounts instead of Docker Volumes
- Caddy handles SSL termination

# MailMule — BFF Refactor Plan

> **Status:** Planning — no code changes yet.
> **Supersedes:** Portions of `IMPLEMENTATION_PLAN.md` (v1 stack decisions) and one constraint in
> `IMAP-Router-Constraints.md` (see §6 below).

---

## 1. What Is Wrong With the Current Architecture

### 1.1 The Worker is an autonomous agent

`InboxSyncWorker` polls IMAP on a timer, independently decides what changed, and writes a
projection of the inbox into SQLite (`InboxMessage` rows). The Blazor frontend then reads that
projection from the database. Neither side communicates with the other directly.

**Consequence:** The UI is always at least one poll interval behind reality. The default is 20
seconds. This is the "terribly slow" feeling.

### 1.2 The database stores state it should not own

The current SQLite schema includes:

| Table | Purpose | Should stay? |
|---|---|---|
| ASP.NET Identity tables | Auth | ✅ Yes |
| `InboxConfig` | IMAP credentials for intake | ✅ Yes |
| `DestinationMailboxConfig` | IMAP credentials for destinations | ✅ Yes |
| `OperationalSettings` | Undo window, etc. | ✅ Yes |
| `InboxMessage` | UID projection / routing state | ❌ Remove |
| `RoutingAction` | Audit log of routing decisions | ❌ Remove |

`InboxMessage` and `RoutingAction` exist purely to bridge the gap between the Worker's IMAP
knowledge and the Web's need to display and act on messages. In the target architecture, the Worker
holds that state in memory and exposes it directly. The bridge disappears.

### 1.3 Blazor Server reaches into the database directly

`RoutingWorkflowService` and `RoutingExecutionService` both take `MailMuleDbContext` as a direct
dependency. The UI layer makes EF Core calls. This collapses the presentation and persistence
layers, and makes it structurally impossible to host the frontend independently of the backend.

---

## 2. Target Architecture

```
┌──────────────────────────────────────────────────────────┐
│                     MailMule.Web                          │
│  Blazor (Server or WASM — see §5.1)                      │
│  - Pure UI / presentation layer                          │
│  - Zero direct database access                           │
│  - Communicates with Worker via HTTP + SignalR            │
└──────────────────────────┬───────────────────────────────┘
                           │  HTTP (commands) + SignalR (events)
                           ▼
┌──────────────────────────────────────────────────────────┐
│                    MailMule.Worker  (BFF)                 │
│  ASP.NET Core Minimal API  +  BackgroundService          │
│                                                          │
│  In-memory state:                                        │
│    - Current inbox UID list                              │
│    - Message summaries (fetched on demand, cached)       │
│    - Pending route queue (undo window)                   │
│                                                          │
│  API surface:                                            │
│    - GET  /inbox/messages                                │
│    - GET  /inbox/messages/{uid}                          │
│    - POST /inbox/messages/{uid}/route                    │
│    - POST /inbox/messages/{uid}/undo                     │
│    - POST /inbox/messages/{uid}/junk                     │
│    - SignalR hub: push inbox change events to Web        │
│                                                          │
│  Background:                                             │
│    - Maintains IMAP connection (IDLE or poll)            │
│    - Pushes change events to SignalR hub on new/removed  │
└────────────┬────────────────────────┬────────────────────┘
             │ EF Core (read-only      │  MailKit
             │ at runtime for creds)   │
             ▼                         ▼
  ┌──────────────────┐        ┌─────────────────┐
  │  Slim Database   │        │   IMAP Server   │
  │  - Identity      │        │   (SmarterMail) │
  │  - InboxConfig   │        └─────────────────┘
  │  - DestConfig    │
  │  - OpSettings    │
  └──────────────────┘
```

### 2.1 Worker becomes a proper ASP.NET Core host

`MailMule.Worker` adds `WebApplication` alongside its existing `BackgroundService`. It exposes:

- **Minimal API endpoints** for all inbox commands (route, undo, junk).
- **A SignalR hub** so the Blazor frontend receives real-time inbox change notifications without
  polling the API.

The `BackgroundService` is retained — it now owns the IMAP connection and pushes events to the hub
instead of writing rows to a database.

### 2.2 Worker owns all IMAP-related state

The in-memory inbox projection lives entirely inside the Worker process. On startup it loads the
current UID list from IMAP. On restart, it reloads from IMAP. Any pending routes in the undo window
are silently discarded on restart — this is acceptable because the undo window is ≤60 seconds.

### 2.3 Web is a pure frontend

`MailMule.Web` retains ASP.NET Identity (cookie auth) and the admin configuration UI (which still
needs direct DB access for managing credentials). Everything inbox-related is delegated to the
Worker API via `HttpClient` and SignalR.

`RoutingWorkflowService`, `RoutingExecutionService`, `PendingRoutingQueue`, `InboxStateStore`, and
`InboxEventStream` **move to the Worker** or are replaced by the Worker's API + hub contract.

---

## 3. Open Questions — Decisions Required Before Implementation

### Q1 — What is the auth model between Web and Worker?

The Worker will expose HTTP endpoints. They must be protected. Options:

| Option | Notes |
|---|---|
| **A. Shared secret / API key** | Simple. Web sends a header the Worker validates. No user identity flows through. |
| **B. Forward the user's Identity cookie** | Web acts as a proxy and attaches the user's session. Requires the Worker to validate ASP.NET Identity cookies — tight coupling to the Web's Identity store. |
| **C. Worker issues its own JWT; Web authenticates as a service** | Clean separation. Web logs into Worker once on startup; all user actions are attributed server-side. |
| **D. No auth between Web and Worker (localhost/container trust boundary)** | Acceptable if they run in the same Docker network and the Worker is never exposed externally. Simple for v2. |

**Recommendation:** Start with **D** (internal trust), add **A** (API key) before any deployment
that puts the Worker port on a routable interface.

### Q2 — Should `RoutingAction` audit log be kept?

The audit log records: user ID, message UID, destination, timestamp, result. It contains no email
content. It is useful for operational debugging ("who routed what and when?").

Options:

- **Keep in DB:** Does not violate the privacy constraint (no email content). Small table. Useful.
- **Remove entirely:** Simplifies the schema. Consistent with the "DB = credentials only" vision.
- **Keep in Worker memory (ring buffer):** Available during the session, lost on restart.

**Decision needed.**

### Q3 — Blazor Server or Blazor WebAssembly?

Removing direct DB access from `MailMule.Web` unblocks a move to Blazor WASM, but it is not
required. The tradeoffs:

| | Blazor Server | Blazor WASM |
|---|---|---|
| Auth | Cookie-based, natural for Identity | Requires token-based auth; more complex |
| Real-time | SignalR already on the server | SignalR also works from WASM |
| Deployment | Single process, needs persistent connection | Can be statically hosted; thinner runtime |
| Complexity | Lower for v2 | Higher — new auth plumbing |

**Recommendation:** Keep Blazor Server for v2. The motivating complaint is DB coupling, not the
rendering model. WASM is a separate future concern.

### Q4 — Does `OperationalSettings` stay in the Web's DB or move to the Worker?

`OperationalSettings` (undo window, etc.) is currently read by `RoutingWorkflowService` which is
moving to the Worker. The Worker needs this setting. Options:

- **Worker reads it from the shared DB** — simple, already how it works today.
- **Web pushes it to the Worker on change** — cleaner boundary but more plumbing.
- **Move it to Worker config / appsettings** — no DB round-trip, but loses the admin UI live-edit.

**Recommendation:** Worker reads from DB (as today). This is a one-time read per routing action —
no performance concern.

### Q5 — What replaces `InboxMessage` for in-flight routing state?

Currently `InboxMessage.State = Routing` is the source of truth for "a route is pending". In the
new model this must live in the Worker's `PendingRoutingQueue` (in memory). The queue already
exists in `MailMule.Web.Services` — it moves to the Worker.

The only risk: if the Worker crashes during the undo window, the in-flight item is lost. The
message remains in the intake IMAP folder and reappears in the next sync. The operator would need
to re-route it. This is acceptable.

---

## 4. What Changes — Project-by-Project

### `MailMule.Core`

- No structural changes.
- `IInboxService`, `IDestinationMailboxService`, `IImapClientFactory` remain as-is.
- Consider adding an interface for the in-memory inbox state store if needed for testing.

### `MailMule.Imap`

- No changes. Implementations stay.

### `MailMule.Persistence`

- **Remove** `InboxMessage`, `InboxMessageState`, `RoutingAction`, `RoutingActionType`,
  `RoutingActionResult` entities (pending Q2 decision on audit log).
- **Regenerate** EF Core migrations for both providers.
- `MailMuleDbContext` becomes a slim credentials-and-config context.

### `MailMule.Worker`

- **Add** `WebApplication` host alongside `BackgroundService`.
- **Add** Minimal API endpoints (see §2.1).
- **Add** SignalR hub.
- **Move in** `RoutingWorkflowService`, `RoutingExecutionService`, `PendingRoutingQueue`,
  and related types from `MailMule.Web`.
- `InboxSyncWorker` no longer writes to DB — pushes events to the SignalR hub instead.

### `MailMule.Web`

- **Remove** `RoutingWorkflowService`, `RoutingExecutionService`, `PendingRoutingQueue`,
  `InboxStateStore`, `InboxEventStream` (logic moves to Worker).
- **Add** typed `HttpClient` wrappers for the Worker API.
- **Add** SignalR client subscription.
- Admin configuration UI retains direct DB access (credentials management).
- ASP.NET Identity stays here.

### Tests

- `MailMule.Integration.Tests`: `WorkerSyncTests` and `RoutingWorkflowServiceTests` need
  significant rewrite — the DB projection is gone, replaced by in-memory state and IMAP mocks.
- New integration tests needed for Worker API endpoints.

---

## 5. Documents That Need Updating After Implementation

| Document | Change Required |
|---|---|
| `IMPLEMENTATION_PLAN.md` | v1 stack table: remove Blazor Server note; update solution structure |
| `IMAP-Router-Constraints.md` | §1 Privacy: remove "routing state and timestamps" from the list of permitted persisted fields |
| `IMPLEMENTATION_PLAN.md` | Phase descriptions reference `InboxMessage` DB sync — obsolete |

---

## 6. Proposed Implementation Phases

### Phase A — Strip the database

Remove `InboxMessage` and `RoutingAction` from the schema. Regenerate migrations. This is a
breaking change that will kill the existing Worker and integration tests — do it first so
everything broken is visible immediately.

### Phase B — In-memory inbox state in the Worker

Replace the DB projection with an in-memory `InboxStateStore` inside the Worker. The
`BackgroundService` populates and maintains it. No API yet — just the state being correct.

### Phase C — Worker grows an API

Add `WebApplication`, Minimal API endpoints, and the SignalR hub to the Worker. Move routing
services in from `MailMule.Web`.

### Phase D — Web becomes a client

Replace all direct DB + service calls in Blazor components with HTTP/SignalR calls to the Worker.
Remove the now-dead services from `MailMule.Web`.

### Phase E — Tests

Rewrite integration tests to target the new architecture. Add API endpoint tests.

---

## 7. Immediate Next Step

**Resolve Q1–Q5 above before writing any code.**

The most consequential decisions are **Q2** (audit log) and **Q1** (auth model), because they
determine the final shape of both the database schema and the Worker's API contract.


Q1: It needs to follow the BFF pattern. The frontend needs to authenticate and authorize the user, but it can do so 
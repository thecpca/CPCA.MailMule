# CPCA MailMule Design Discussion

> I need to build an "IMAP Router". Basically, the CPCA wants all email to come into a single "general purpose" IMAP mailbox and then a user interface will allow a staff member to read each email and then route it to the appropriate person (another IMAP Mailbox).
> 
> The important bit is that whoever ends up with the email can simply click reply and the reply will come from them and go to the original sender. This is why we cannot simply forward the email, as then whoever receives it will be replying to the forwarder and not the original sender.
> 
> On the surface this does not seem like a very complicated app. Configure one "Inbox" and all the "Outboxes", and then provide the incoming message list and nice large buttons to make routing to the "outboxes" a single click.

Here is the good news: what you are describing is not only feasible, it is a pattern that has been implemented in many help‑desk and intake systems for decades. The twist is that you want to preserve the *original envelope sender* so that replies behave naturally. That constraint is what rules out simple forwarding, but it does not make the architecture difficult.

Let me walk you through a clean, durable design that fits your style: explicit, deterministic, enforceable, and easy to reason about.

---

# 📨 IMAP Router: A Solid, Minimal Architecture

## 🎯 Core Requirements
- One “general intake” IMAP mailbox.
- A staff UI that shows incoming messages.
- A one‑click “route to mailbox X” action.
- The routed message must appear in the destination mailbox as if it had been delivered there originally.
- When the recipient clicks **Reply**, the reply must:
  - Come from *their* mailbox identity.
  - Go to the *original sender*, not the intake mailbox.

This last point is the entire reason you cannot use forwarding.

---

# 🧠 Key Insight: You Do Not Forward — You **Re‑Inject**

Instead of forwarding, you **copy the original MIME message** (unaltered) into the target mailbox using IMAP `APPEND`.

This preserves:
- The original `From`
- The original `To`
- The original `Message-ID`
- All headers and attachments
- The original MIME structure

When the staff member opens the message in their own mailbox, their mail client will naturally use:
- Their mailbox identity as the sender
- The original `From` as the reply target

This is exactly how help‑desk systems like Zendesk, Freshdesk, and many ticketing systems behave when they “assign” an email to an agent.

---

# 🧩 Architecture Breakdown

## 1. **IMAP Intake Poller**
A small service (Aspire worker, background service, or container) that:
- Connects to the intake IMAP mailbox
- Fetches unread messages
- Stores them in your database
- Marks them as processed (or moves them to an “Archived” folder)

This is straightforward IMAP work.

## 2. **Routing UI**
A simple Blazor page:
- List of unassigned messages
- Preview pane
- Buttons for each destination mailbox (“Route to Registrar”, “Route to Ethics”, etc.)

Clicking a button triggers a backend call:
- Fetch the stored MIME blob
- Connect to the destination IMAP mailbox
- `APPEND` the message into the Inbox

## 3. **IMAP Outbox Writer**
This is the key component:
- Connect to the target mailbox using its credentials
- Use IMAP `APPEND` to insert the raw MIME message
- Optionally set flags (`\Seen`, `\Recent`, etc.)

No rewriting. No forwarding. No header manipulation.

This preserves reply behavior perfectly.

---

# 🛡️ Compliance & Security Notes (PIPEDA/PIPA/HIPAA)
This design is compliant-friendly because:
- You never impersonate a sender.
- You never rewrite headers.
- You never spoof SMTP envelopes.
- You never store credentials in the client; everything is server-side.
- You can enforce MFA and app passwords for each mailbox.

If you want to go further:
- Use OAuth2 for IMAP (Microsoft 365 supports it).
- Store MIME blobs encrypted at rest.
- Log routing actions for auditability.

---

# 🧪 Operational Considerations
- **Idempotency**: Ensure routing is atomic so you do not double‑route.
- **Concurrency**: Lock a message when a staff member opens it.
- **Mailbox credentials**: Use per‑mailbox secrets in Key Vault.
- **Scalability**: Intake volume is low; a single worker is fine.
- **Testing**: Use GreenMail or MailHog for local IMAP simulation.

---

# 🧭 Why This Works So Well
Because IMAP is fundamentally a *message store*, not a transport.  
If you insert a message into a mailbox, the mail client treats it as if it arrived there naturally.

You are not “forwarding”.  
You are “placing the original message where it should have gone”.

This is the cleanest possible solution.

---

# 💡 Optional Enhancements
- Auto‑suggest routing based on keywords or sender domain.
- Add a “return to intake” button.
- Add a “route and notify” workflow.
- Add a “claim” system so two staff do not route the same message.

---

# 🧵 My Take
This is a classic Doug project: small surface area, high clarity, and a perfect fit for your architecture instincts. The IMAP `APPEND` trick is the whole game. Once you embrace that, the rest is just a tidy little Blazor + background worker app.

If you want, I can sketch:
- A full component diagram
- A sample IMAP `APPEND` implementation in C#
- A suggested database schema
- A UI mockup for the routing screen

Just tell me which direction you want to explore next.

> What I need is an "implementation plan" that I can hand off to GitHub Copilot and then supervise the implementation step by step, phase by phase, or whatever breakdown will provide the highest quality result. I do not mind getting my hands dirty and writing some of the code if that is the best path forward.
> 
> - I am not in a big hurry. As always, I want it done right more than I want it done fast.
> - Good point about PIPEDA/PIPA/HIPAA. Email messages themselves must never be stored outside of the IMAP servers involved.
> - It needs to be highly reliable. "losing" an email is not an option.
> - It needs a mechanism to handle spam, likely by moving it to an existing spam or junk folder provided by the IMAP server.
> - It should not matter, but the IMAP server is SmarterMail
> 
> Any questions or clarifications?

**Outlining IMAP router phases**

I’m thinking of breaking the project into phases: 

1. Requirements & IMAP capabilities check (SmarterMail flags, special folders).
2. Skeleton solution: .NET worker + web UI.
3. IMAP abstraction layer (MailKit) with intake polling and metadata projection.
4. Persistence: DB schema for messages (UID, mailbox, hash, state).
5. Routing workflow with transactional algorithm.
6. Spam handling.
7. Reliability & monitoring.
8. Security & config.
9. UX polish.

I’ll also mention that Copilot will be guided with explicit tasks per phase. I need to emphasize no email content in the DB—only minimal metadata and IMAP UID. The UI will fetch the body live from IMAP. I'll also note concurrency, deduplication, and audit logging.
You’re speaking my language here—“done right”, privacy‑tight, and supervised instead of “magic it together”.

Let me give you a concrete implementation plan you can hand to GitHub Copilot and then drive phase by phase.

---

### High‑level implementation phases

| Phase | Goal | Primary Output |
|-------|------|----------------|
| 0 | Confirm constraints & IMAP capabilities | Checked assumptions, config model |
| 1 | Solution skeleton | Worker + Web UI + shared core |
| 2 | IMAP abstraction | Clean IMAP service layer (intake + target) |
| 3 | Persistence & state model | DB schema for message tracking (no bodies) |
| 4 | Routing workflow | Reliable “intake → target” routing pipeline |
| 5 | Spam handling | Spam/Junk routing and UI affordances |
| 6 | Reliability & monitoring | Idempotency, logging, alerts |
| 7 | Security & configuration | Secrets, roles, audit trail |
| 8 | UX polish & guardrails | Smooth operator experience |

Below is how I’d structure each phase so Copilot has crisp, bounded tasks.

---

## Phase 0: Constraints and IMAP capabilities

**Goals:**

- Lock in non‑negotiables:
  - **No email bodies or full MIME stored outside IMAP.**
  - Only transient in memory when rendering UI or routing.
- Confirm SmarterMail specifics:
  - Special folder names (e.g., `Junk E-mail`, `Spam`, `Deleted Items`).
  - IMAP capabilities: IDLE, UIDPLUS, MOVE, etc.

**Tasks for Copilot:**

- **Task:** Generate a short markdown “IMAP Router Technical Constraints” doc:
  - No persistent storage of MIME or body.
  - Only store:
    - **Mailbox identifier**
    - **UID / UIDVALIDITY**
    - **Minimal metadata** (we can decide how minimal—see below)
    - **State** (New, InReview, Routed, Spam, Error)
  - All message display must be live from IMAP.

You can then tweak that doc manually to match PIPEDA/PIPA/HIPAA expectations.

> **Privacy nuance:** Even subject lines and sender addresses can be PHI. If you want to be strict, store only:
> - Intake mailbox ID
> - UID
> - State
> - Timestamps  
> and fetch everything else live from IMAP. The UI will be slightly more chatty, but fully compliant.

---

## Phase 1: Solution skeleton

**Goals:**

- Create a clean .NET solution layout that Copilot can fill in.

**Suggested structure:**

- `ImapRouter.sln`
  - `ImapRouter.Core` (domain models, interfaces)
  - `ImapRouter.Imap` (IMAP implementation)
  - `ImapRouter.Worker` (intake poller / router background service)
  - `ImapRouter.Web` (Blazor Server or MVC UI)
  - `ImapRouter.Persistence` (EF Core or Dapper)

**Tasks for Copilot:**

- **Task:** Scaffold a .NET solution with the above projects and references.
- **Task:** Add basic dependency injection wiring in `Worker` and `Web` using `ImapRouter.Core` abstractions.

You can review the project layout and adjust naming before moving on.

---

## Phase 2: IMAP abstraction layer

**Goals:**

- Encapsulate all IMAP operations behind interfaces so you can swap libraries or mock easily.
- Likely use **MailKit** for SmarterMail.

**Core interfaces (in `ImapRouter.Core`):**

- `IImapAccount`  
  - **Properties:** `Host`, `Port`, `UseSsl`, `Username`, `AuthType`, etc.
- `IImapClientFactory`
- `IIntakeMailboxService`
  - Methods like:
    - `Task<IReadOnlyList<ImapMessageHeader>> GetNewMessagesAsync(...)`
    - `Task<Stream> GetMessageBodyAsync(uid)`
- `ITargetMailboxService`
  - `Task AppendMessageAsync(uid, targetMailboxId, CancellationToken)`
- `IImapRoutingService`
  - High‑level operations: `RouteMessageAsync(messageId, targetMailboxId)`

**Tasks for Copilot:**

- **Task:** Implement `ImapMessageHeader` model (minimal metadata).
- **Task:** Implement `MailKitImapClientFactory` and basic connect/auth logic.
- **Task:** Implement `IntakeMailboxService`:
  - List messages in the intake folder.
  - Fetch headers and UIDs.
  - Fetch full message stream on demand (no persistence).

You then test against SmarterMail with a test mailbox.

---

## Phase 3: Persistence and state model

**Goals:**

- Track messages and routing state without storing content.

**Suggested schema (conceptual):**

- **Table: `IntakeMessage`**
  - `Id` (GUID)
  - `IntakeMailboxId` (FK to `MailboxConfig`)
  - `ImapUid` (long)
  - `ImapUidValidity` (long)
  - `State` (enum: New, InReview, Routed, Spam, Error)
  - `CreatedUtc`
  - `LastUpdatedUtc`
  - Optional minimal metadata if you’re comfortable:
    - `FromDisplay`
    - `FromAddress`
    - `Subject`  
    (or skip these for maximum privacy)

- **Table: `MailboxConfig`**
  - `Id`
  - `DisplayName`
  - `ImapHost`, `ImapPort`, `UseSsl`
  - `Username` (or reference to secret)
  - `FolderIntake`, `FolderSpam`, `FolderArchive`

- **Table: `RoutingAction`**
  - `Id`
  - `IntakeMessageId`
  - `ActionType` (Route, MarkSpam, Restore, etc.)
  - `TargetMailboxId` (nullable)
  - `PerformedByUserId`
  - `PerformedAtUtc`
  - `Result` (Success, Failed, etc.)

**Tasks for Copilot:**

- **Task:** Create EF Core models and migrations for the above schema.
- **Task:** Implement repositories or query services for `IntakeMessage` and `RoutingAction`.

You can then review the migration script to ensure no content columns slipped in.

---

## Phase 4: Routing workflow (the heart of it)

**Goals:**

- Implement a **reliable, idempotent routing algorithm**:
  - Never lose an email.
  - Never route twice accidentally.
  - Keep intake mailbox as the source of truth.

**Routing algorithm (per message):**

1. **Precondition:** `IntakeMessage.State == New` (or `InReview`).
2. **Step 1:** Fetch message from intake IMAP by UID.
3. **Step 2:** Append message to target mailbox via IMAP `APPEND`.
4. **Step 3:** Verify success (e.g., by listing last appended UID or relying on success response).
5. **Step 4:** Update `IntakeMessage.State = Routed` and record `RoutingAction`.
6. **Step 5:** Optionally move original message in intake mailbox to an `Archive` folder (IMAP `MOVE` or `COPY+DELETE`).

> If any step fails, log and keep `State` as `New` or `Error`—never delete from intake until you’re sure it exists in the target mailbox.

**Tasks for Copilot:**

- **Task:** Implement `IImapRoutingService.RouteMessageAsync(intakeMessageId, targetMailboxId)`.
- **Task:** Make routing idempotent:
  - If `State == Routed`, do nothing.
  - If `State == Error`, allow retry.
- **Task:** Add unit tests for:
  - Success path.
  - Failure during `APPEND`.
  - Failure during archive move.

You can manually review the routing code to ensure no content is persisted.

---

## Phase 5: Spam handling

**Goals:**

- Provide a way to mark messages as spam and move them to the IMAP spam/junk folder.
- Optionally auto‑detect spam based on existing SmarterMail flags.

**Approach:**

- **Manual spam marking:**
  - UI button: “Mark as spam”.
  - Backend:
    - IMAP `MOVE` from intake folder to spam/junk folder.
    - Update `IntakeMessage.State = Spam`.
    - Log `RoutingAction` with `ActionType = Spam`.

- **Leverage SmarterMail spam classification:**
  - Intake poller can:
    - Skip messages already in spam/junk.
    - Or import them into a separate “Spam review” view if you want.

**Tasks for Copilot:**

- **Task:** Extend `MailboxConfig` with `FolderSpam`.
- **Task:** Implement `MarkAsSpamAsync(intakeMessageId)` in routing service.
- **Task:** Add UI button and wire it to the backend.

---

## Phase 6: Reliability, monitoring, and intake worker

**Goals:**

- Ensure no messages are missed.
- Provide visibility into failures.

**Intake worker behavior:**

- Poll intake mailbox on a schedule (e.g., every 30–60 seconds).
- For each message in the intake folder:
  - If no `IntakeMessage` exists for `(MailboxId, UID, UIDVALIDITY)`:
    - Create `IntakeMessage` with `State = New`.
- Optionally use IMAP IDLE if SmarterMail supports it reliably.

**Reliability features:**

- **Idempotent polling:** Never assume “seen” means “processed”; rely on DB state.
- **Error handling:** Any IMAP failure logs an error and leaves state unchanged.
- **Health checks:** Expose `/health` endpoint that:
  - Verifies DB connectivity.
  - Verifies IMAP connectivity to intake mailbox.

**Tasks for Copilot:**

- **Task:** Implement `IntakePollingService` in `Worker`:
  - Timer‑based loop.
  - Uses `IIntakeMailboxService` and persistence layer.
- **Task:** Add structured logging (Serilog or built‑in) with correlation IDs per message.

---

## Phase 7: Security, configuration, and audit

**Goals:**

- Keep secrets out of code.
- Provide an audit trail for compliance.

**Configuration:**

- Store IMAP credentials in:
  - Environment variables, or
  - Azure Key Vault / similar.
- `MailboxConfig` can reference a secret name rather than storing passwords.

**Audit:**

- `RoutingAction` table already gives you:
  - Who routed what, when, and where.
- Add:
  - `User` table or integrate with your existing identity system.
  - Role checks: only authorized staff can route.

**Tasks for Copilot:**

- **Task:** Wire configuration binding for `MailboxConfig` from appsettings + secrets.
- **Task:** Implement a simple auth layer in `Web` (even if just placeholder for now).
- **Task:** Add audit logging when routing/spam actions occur.

---

## Phase 8: UI/UX for the router

**Goals:**

- Make it easy and safe for staff to route messages.

**Core screens:**

- **Inbox view:**
  - List of `IntakeMessage` in `State = New` or `InReview`.
  - Columns: From, Subject, Received time (fetched live from IMAP or cached minimally).
- **Message view:**
  - When a message is selected:
    - Backend fetches body from IMAP by UID.
    - Render HTML safely (sanitization).
  - Buttons:
    - “Route to [Mailbox X]” (one button per configured mailbox).
    - “Mark as spam”.
    - Optional: “Mark as done” without routing.

**Tasks for Copilot:**

- **Task:** Scaffold a Blazor page with:
  - Left pane: message list.
  - Right pane: message preview + action buttons.
- **Task:** Implement API endpoints in `Web` that:
  - List messages (from DB).
  - Fetch message body (from IMAP).
  - Trigger routing/spam actions.

You can then refine the UX and add guardrails (confirmation dialogs, disabled buttons while routing, etc.).

---

## How to drive Copilot with this plan

For each phase, you can literally paste a **single, focused task block** into Copilot Chat, e.g.:

> “In the `ImapRouter.Core` project, create interfaces `IImapAccount`, `IImapClientFactory`, `IIntakeMailboxService`, and `ITargetMailboxService` as described below: …”

Then review, adjust, and move to the next task. You stay in the architect/reviewer seat, Copilot does the scaffolding and boilerplate.

---

## A couple of clarifications for you

1. **Privacy strictness:** Do you want to avoid storing even `Subject` and `From` in the DB, or are you comfortable treating those as “operational metadata” under your existing agreements?
2. **Auth model for staff:** Are you planning to plug this into your existing counsellor/clinic auth, or is this a separate internal tool with its own user store?

Your answers to those two will slightly tweak the persistence and UI design, but the overall plan stands as‑is.

> 1. I would prefer not to store any details about the emails unless it is temporary. For example, I almost always use SEQ for log aggregation. It is okay for the Sender, Date, Subject, and Receiver to be logged to SEQ as the logs are only retained for 30 days. As far as the application is concerned, once an email message has been routed, it should no longer know anything about it. It is worth mentioning that existing messages in the destination IMAP folders must never be read. The destination IMAP folders should be treated as black holes. Stuff can go in, but as far as this application is concerned, nothing ever comes back out.
>
> 2. It does not exist yet, but there plans on the horizon to support SSO for both staff and members. Might as well plan on integrating with that, even if it requires those future plans to become more immediate.

Below is a refined, privacy‑tight, reliability‑first implementation plan that incorporates your two clarifications. I am going to give you a version that is *ready to hand to GitHub Copilot*, phase by phase, with explicit constraints that will keep Copilot from drifting into unsafe territory (like storing message bodies or reading destination mailboxes).

I will also adjust the architecture to reflect your “destination mailboxes are black holes” rule and your future SSO requirement.

---

# 🧭 Updated Architectural Principles (Based on Your Clarifications)

### 🔒 1. **No persistent storage of email content**
- No MIME bodies.
- No headers.
- No subject lines.
- No sender/recipient addresses.
- No message metadata of any kind in the database.

### 🧹 2. **Temporary metadata is allowed only in logs**
- SEQ logs may include:
  - Sender
  - Recipient
  - Subject
  - Date  
- Logs expire in 30 days, which satisfies your privacy posture.

### 🕳️ 3. **Destination mailboxes are write‑only**
- The application must **never**:
  - List messages in destination mailboxes.
  - Fetch messages from destination mailboxes.
  - Inspect destination mailbox contents.
- Only allowed operation: **IMAP APPEND**.

### 🧩 4. **Future SSO integration**
- The Web UI must be built with an authentication abstraction that can later plug into:
  - OIDC
  - SAML
  - Azure AD / Entra ID
  - Or your future CPCA SSO provider

### 🧱 5. **Reliability is paramount**
- Intake mailbox is the *source of truth*.
- A message is only removed from intake after:
  - Successful `APPEND` to destination, and
  - Successful `MOVE` to intake archive folder.

### 🛡️ 6. **Compliance**
- No PHI stored.
- No PHI cached.
- No PHI written to disk.
- All message content is fetched live from IMAP only when needed for UI display.

---

# 🏗️ Revised Implementation Plan (Copilot‑Ready)

Below is the full plan, rewritten to reflect your clarified constraints.  
Each phase includes **explicit instructions** you can paste into Copilot.

---

# **Phase 0 — Requirements & IMAP Capability Validation**

### Goals
- Document constraints.
- Validate SmarterMail IMAP capabilities (UIDPLUS, MOVE, IDLE).

### Copilot Task
> Create a markdown document named `IMAP-Router-Constraints.md` summarizing the following:
> - No email content or metadata stored in the database.
> - Only store: IntakeMailboxId, UID, UIDVALIDITY, State, timestamps.
> - Destination mailboxes are write‑only; never read from them.
> - All message display must fetch content live from IMAP.
> - Routing must be idempotent and reliable.
> - Logs may contain temporary metadata (subject, from, to).
> - Future SSO integration required.

---

# **Phase 1 — Solution Skeleton**

### Goals
- Create a clean .NET solution with clear boundaries.

### Copilot Task
> Scaffold a .NET solution with the following projects:
> - `ImapRouter.Core`
> - `ImapRouter.Imap`
> - `ImapRouter.Persistence`
> - `ImapRouter.Worker`
> - `ImapRouter.Web`
> Add DI wiring and project references but no business logic yet.

---

# **Phase 2 — IMAP Abstraction Layer**

### Goals
- Encapsulate all IMAP operations.
- Ensure no accidental reading of destination mailboxes.

### Required Interfaces
- `IIntakeMailboxService`
- `IMessageFetchService` (fetch body for UI)
- `IRoutingMailboxService` (APPEND only)
- `IImapClientFactory`

### Copilot Task
> Implement the IMAP abstraction layer using MailKit with the following rules:
> - Intake mailbox service may list messages and fetch bodies.
> - Routing mailbox service may only perform `APPEND` and must not list or fetch.
> - All IMAP operations must be cancellable and logged.
> - No message content is ever persisted.

---

# **Phase 3 — Persistence Layer (Metadata‑Only)**

### Goals
- Track routing state without storing PHI.

### Schema
**Table: IntakeMessage**
- `Id` (GUID)
- `MailboxId`
- `Uid`
- `UidValidity`
- `State` (New, Routed, Spam, Error)
- `CreatedUtc`
- `UpdatedUtc`

**Table: RoutingAction**
- `Id`
- `IntakeMessageId`
- `ActionType`
- `TargetMailboxId`
- `PerformedByUserId`
- `PerformedAtUtc`
- `Result`

### Copilot Task
> Implement EF Core models and migrations for the above schema.
> Do not add any columns for subject, sender, or other metadata.

---

# **Phase 4 — Routing Workflow (Transactional, Idempotent)**

### Goals
- Guarantee no lost messages.
- Guarantee no double routing.
- Guarantee no reading from destination mailboxes.

### Algorithm
1. Fetch message from intake by UID.
2. `APPEND` to destination mailbox.
3. Verify success (MailKit success response).
4. `MOVE` original message to intake archive folder.
5. Update DB state to `Routed`.
6. Log metadata to SEQ (temporary).

### Copilot Task
> Implement `IImapRoutingService.RouteMessageAsync` with the following rules:
> - Never read from destination mailboxes.
> - Never delete from intake until APPEND succeeds.
> - If APPEND fails, leave state unchanged.
> - If MOVE fails, mark state as Error but do not retry APPEND.
> - Log sender/subject/date to SEQ but do not persist them.

---

# **Phase 5 — Spam Handling**

### Goals
- Allow staff to mark a message as spam.
- Move message to intake spam folder.

### Copilot Task
> Implement `MarkAsSpamAsync`:
> - IMAP MOVE from intake folder to spam folder.
> - Update state to Spam.
> - Log metadata to SEQ.
> - Do not store metadata in DB.

---

# **Phase 6 — Intake Worker (Reliable Polling)**

### Goals
- Detect new messages.
- Never miss a message.
- Never process a message twice.

### Copilot Task
> Implement a background polling service:
> - Poll intake folder every 30–60 seconds.
> - For each UID not in DB, create a new IntakeMessage with State=New.
> - Do not store metadata.
> - Use UIDVALIDITY to detect mailbox resets.
> - Add structured logging for each poll cycle.

---

# **Phase 7 — Authentication & Authorization (SSO‑Ready)**

### Goals
- Build an auth abstraction that can plug into future SSO.

### Copilot Task
> Implement an authentication layer in `ImapRouter.Web`:
> - Use ASP.NET Core Identity or a custom `IUserContext` abstraction.
> - Add role-based authorization for routing actions.
> - Ensure the design can later plug into OIDC/SAML without major refactoring.

---

# **Phase 8 — Web UI (Blazor Server)**

### Goals
- Provide a clean, safe UI for routing.

### Rules
- Message list is populated from DB (UID only).
- Message preview is fetched live from IMAP.
- Routing buttons trigger backend actions.
- No message content is cached or stored.

### Copilot Task
> Implement a Blazor UI with:
> - Left pane: list of IntakeMessages (UID only).
> - Right pane: message preview fetched live via IMAP.
> - Buttons: Route to X, Mark as Spam.
> - Confirmation dialogs for routing.
> - Loading indicators and error banners.

---

# **Phase 9 — Reliability, Monitoring, and Observability**

### Goals
- Ensure operational safety.
- Provide visibility into failures.

### Copilot Task
> Add:
> - Health checks for IMAP connectivity.
> - Logging of all routing actions.
> - Error alerts for failed APPEND or MOVE.
> - Correlation IDs for each message.

---

# 🧩 Final Clarifications I Need From You

To finalize the plan and generate the Copilot‑ready task list:

### 1. **Do you want the UI to show message metadata (subject, from, date) before the user clicks to preview the message?**  
This requires fetching headers live from IMAP for each message in the list.  
Still compliant, still ephemeral, but slightly more IMAP traffic.

### 2. **Do you want the intake worker to detect deleted messages?**  
If someone manually deletes a message from the intake mailbox, should the system:
- Mark it as “Externally Removed”, or
- Ignore it entirely?

### 3. **Do you want routing to be single‑step (route immediately) or two‑step (claim → route)?**  
Two‑step prevents two staff from routing the same message simultaneously.

---

If you answer those three, I can produce the final “Copilot Implementation Script” — a single document you can paste into Copilot to drive the entire build from start to finish.

> 1. The user doing the routing should see a view comparable to any existing email application, including reading the email if they select it. Instead of Reply, Forward, Delete, etc., they have buttons for where to route it including "Junk". Currently it is expected that there will be around 15 "destinations". This likely will increase in the future so vNext may need to add some form of organizing and searching of the destinations. v1 may rely on the "around 15 destinations".
>
> 2. No. The "intake view" is always real-time. If a message was there and is now gone, then it is gone. It should just disappear from the UI and the user can wonder if they are hallucinating. Incidentally, there should only be one user doing routing. It is part of their job description, we are just making it easier to do the job.
>
> 3. Route immediately. There should be some type of confirmation timeout that allows the user to change their mind for x number of seconds. Like when you hit "Shutdown" on a Linux desktop, it defaults to a 60 second delay than can be cancelled or short circuited with a "Shutdown Now".

Perfect — your clarifications lock the last pieces into place, and they actually simplify the architecture in a few important ways. With these constraints, we can finalize the **v1 implementation plan** and ensure it is something you can hand to GitHub Copilot and supervise phase‑by‑phase.

Below is the **fully updated plan**, incorporating:

- A real‑time intake view that behaves like a normal email client  
- A write‑only routing model for destination mailboxes  
- No persistent storage of PHI  
- A single human operator  
- Immediate routing with a cancel window  
- Future SSO integration  
- A scalable destination list (v1 fixed ~15, vNext searchable)

This is the version you can use as your “Copilot Implementation Script”.

---

# 🧭 Final Implementation Plan (v1, Copilot‑Ready)

## **Core Principles (Non‑Negotiable)**

### 🔒 Privacy
- No email content or metadata stored in the database.
- Only temporary metadata in logs (SEQ, 30‑day retention).
- All message content fetched live from IMAP.

### 🕳️ Destination Mailboxes
- Treated as **write‑only black holes**.
- Only allowed operation: `APPEND`.
- Never list, fetch, or inspect destination folders.

### 📬 Intake Mailbox
- Source of truth.
- UI reflects real‑time state.
- If a message disappears (deleted externally), it simply vanishes from the UI.

### 👤 Single Operator
- No concurrency concerns.
- No “claim” workflow needed.

### ⚡ Routing UX
- Immediate routing with a **cancel window** (e.g., 10–60 seconds).
- “Undo” cancels the routing job before it executes.

---

# 🏗️ Phase‑by‑Phase Build Plan

Each phase includes **explicit Copilot tasks** you can paste into your IDE.

---

# **Phase 0 — Constraints & IMAP Capability Validation**

### Goals
- Document constraints.
- Validate SmarterMail IMAP capabilities (UIDPLUS, MOVE, IDLE).

### Copilot Task
> Create a markdown document named `IMAP-Router-Constraints.md` summarizing:
> - No email content or metadata stored in the database.
> - Only store: IntakeMailboxId, UID, UIDVALIDITY, State, timestamps.
> - Destination mailboxes are write‑only; never read from them.
> - All message display must fetch content live from IMAP.
> - Routing is immediate with a cancel window.
> - Logs may contain temporary metadata.
> - Future SSO integration required.

---

# **Phase 1 — Solution Skeleton**

### Goals
- Create a clean .NET solution with clear boundaries.

### Copilot Task
> Scaffold a .NET solution with the following projects:
> - `ImapRouter.Core`
> - `ImapRouter.Imap`
> - `ImapRouter.Persistence`
> - `ImapRouter.Worker`
> - `ImapRouter.Web`
> Add DI wiring and project references but no business logic yet.

---

# **Phase 2 — IMAP Abstraction Layer**

### Goals
- Encapsulate all IMAP operations.
- Enforce “destination is write‑only”.

### Required Interfaces
- `IIntakeMailboxService` (list + fetch)
- `IMessageFetchService` (fetch body for UI)
- `IRoutingMailboxService` (APPEND only)
- `IImapClientFactory`

### Copilot Task
> Implement the IMAP abstraction layer using MailKit with the following rules:
> - Intake mailbox service may list messages and fetch bodies.
> - Routing mailbox service may only perform `APPEND` and must not list or fetch.
> - All IMAP operations must be cancellable and logged.
> - No message content is ever persisted.

---

# **Phase 3 — Persistence Layer (Metadata‑Only)**

### Goals
- Track routing state without storing PHI.

### Schema
**Table: IntakeMessage**
- `Id` (GUID)
- `MailboxId`
- `Uid`
- `UidValidity`
- `State` (New, Routed, Spam, Error)
- `CreatedUtc`
- `UpdatedUtc`

**Table: RoutingAction**
- `Id`
- `IntakeMessageId`
- `ActionType`
- `TargetMailboxId`
- `PerformedByUserId`
- `PerformedAtUtc`
- `Result`

### Copilot Task
> Implement EF Core models and migrations for the above schema.
> Do not add any columns for subject, sender, or other metadata.

---

# **Phase 4 — Routing Workflow (Transactional, Idempotent)**

### Goals
- Guarantee no lost messages.
- Guarantee no double routing.
- Guarantee no reading from destination mailboxes.

### Algorithm
1. Fetch message from intake by UID.
2. `APPEND` to destination mailbox.
3. Verify success (MailKit success response).
4. `MOVE` original message to intake archive folder.
5. Update DB state to `Routed`.
6. Log metadata to SEQ (temporary).

### Copilot Task
> Implement `IImapRoutingService.RouteMessageAsync` with the following rules:
> - Never read from destination mailboxes.
> - Never delete from intake until APPEND succeeds.
> - If APPEND fails, leave state unchanged.
> - If MOVE fails, mark state as Error but do not retry APPEND.
> - Log sender/subject/date to SEQ but do not persist them.

---

# **Phase 5 — Spam Handling**

### Goals
- Allow staff to mark a message as spam.
- Move message to intake spam folder.

### Copilot Task
> Implement `MarkAsSpamAsync`:
> - IMAP MOVE from intake folder to spam folder.
> - Update state to Spam.
> - Log metadata to SEQ.
> - Do not store metadata in DB.

---

# **Phase 6 — Intake Worker (Real‑Time Polling)**

### Goals
- Detect new messages.
- Never miss a message.
- Never process a message twice.
- Reflect real‑time state in UI.

### Copilot Task
> Implement a background polling service:
> - Poll intake folder every 10–30 seconds.
> - For each UID not in DB, create a new IntakeMessage with State=New.
> - If a UID disappears from IMAP, remove it from the UI (do not track deletions).
> - Use UIDVALIDITY to detect mailbox resets.
> - Add structured logging for each poll cycle.

---

# **Phase 7 — Authentication & Authorization (SSO‑Ready)**

### Goals
- Build an auth abstraction that can plug into future SSO.

### Copilot Task
> Implement an authentication layer in `ImapRouter.Web`:
> - Use ASP.NET Core Identity or a custom `IUserContext` abstraction.
> - Add role-based authorization for routing actions.
> - Ensure the design can later plug into OIDC/SAML without major refactoring.

---

# **Phase 8 — Web UI (Blazor Server)**

### Goals
- Provide a clean, safe UI for routing.
- Behave like a normal email client.

### UI Requirements
- Left pane: list of messages (live from IMAP headers).
- Right pane: message preview (live from IMAP body).
- Buttons:
  - Route to Destination X (15 buttons)
  - Mark as Junk
  - Undo (during cancel window)

### Cancel Window
- When user clicks “Route to X”:
  - Show a banner: “Routing to X in 10 seconds… Undo?”
  - If Undo clicked → cancel job.
  - If timeout expires → execute routing.

### Copilot Task
> Implement a Blazor UI with:
> - Left pane: list of IntakeMessages (UID only).
> - For each message, fetch headers live from IMAP.
> - Right pane: message preview fetched live via IMAP.
> - Buttons: Route to X, Mark as Spam.
> - Implement a cancel window using a timer and a pending‑action queue.
> - Add confirmation banners and undo functionality.

---

# **Phase 9 — Reliability, Monitoring, and Observability**

### Goals
- Ensure operational safety.
- Provide visibility into failures.

### Copilot Task
> Add:
> - Health checks for IMAP connectivity.
> - Logging of all routing actions.
> - Error alerts for failed APPEND or MOVE.
> - Correlation IDs for each message.

---

# 🧩 vNext Considerations (Not for v1)

- Destination search/filtering UI.
- Grouping destinations by department.
- Keyboard shortcuts for routing.
- Auto‑suggest routing based on keywords.
- Multi‑operator support with locking.

---

# 🎯 You Now Have a Complete, Copilot‑Ready Implementation Plan

This plan is:

- Privacy‑tight  
- Operationally safe  
- Architecturally clean  
- Easy to supervise  
- Easy for Copilot to execute phase‑by‑phase  
- Future‑proof for SSO and destination growth  

If you want, I can now generate:

- A **single consolidated “Copilot Script”** you can paste into your repo  
- A **component diagram**  
- A **database diagram**  
- A **routing sequence diagram**  
- Or a **step‑by‑step Copilot prompt sequence** for each phase  

Just tell me which one you want next.

> Let's standardize on MudBlazor for UI components. The "step‑by‑step Copilot prompt sequence for each phase" is most valuable to me right now.

Nice—MudBlazor is a great call for this kind of “operator console” UI.

Here’s a **step‑by‑step Copilot prompt sequence**, phase by phase. Think of each block as something you paste into Copilot Chat in your IDE while sitting in the relevant project/file.

---

## Phase 0 – Constraints doc

**Prompt 0.1 – Create constraints document**

> Create a markdown file at the solution root named `IMAP-Router-Constraints.md` with the following points:
> - No email content or metadata (subject, sender, recipient, body) is stored in any database.
> - The only persisted message fields are: `IntakeMailboxId`, `Uid`, `UidValidity`, `State`, `CreatedUtc`, `UpdatedUtc`.
> - Destination IMAP folders are write‑only: the application must never list, search, or fetch messages from them. Only `APPEND` is allowed.
> - All message headers and bodies are fetched live from the intake IMAP mailbox when needed for UI display or routing.
> - Logs (e.g., SEQ) may temporarily contain sender, recipient, subject, and date, with a retention period of 30 days.
> - Routing is immediate but must support a short cancel window (e.g., 10–60 seconds) where the user can undo before the routing operation executes.
> - There is a single human operator responsible for routing; no multi‑user concurrency model is required for v1.
> - The Web UI will be built with Blazor Server and MudBlazor components.
> - Authentication and authorization must be implemented in a way that can later plug into SSO (OIDC/SAML/Entra ID) without major refactoring.

---

## Phase 1 – Solution skeleton

**Prompt 1.1 – Scaffold solution and projects**

> Create a .NET solution named `ImapRouter` with the following projects:
> - `ImapRouter.Core` (class library)
> - `ImapRouter.Imap` (class library)
> - `ImapRouter.Persistence` (class library)
> - `ImapRouter.Worker` (console app / worker service)
> - `ImapRouter.Web` (Blazor Server app)
> Add project references so that:
> - `ImapRouter.Imap`, `ImapRouter.Persistence`, `ImapRouter.Worker`, and `ImapRouter.Web` all reference `ImapRouter.Core`.
> - `ImapRouter.Worker` and `ImapRouter.Web` reference `ImapRouter.Imap` and `ImapRouter.Persistence`.

**Prompt 1.2 – Basic DI wiring**

> In `ImapRouter.Worker` and `ImapRouter.Web`, set up a standard `Host.CreateDefaultBuilder` with dependency injection and logging.
> Register placeholder services for:
> - `IIntakeMailboxService`
> - `IRoutingMailboxService`
> - `IImapRoutingService`
> - `IIntakePollingService`
> Implement them as empty stubs for now so the solution builds.

---

## Phase 2 – IMAP abstraction (MailKit)

**Prompt 2.1 – Define IMAP interfaces**

> In `ImapRouter.Core`, define the following interfaces and models:
> - `IIntakeMailboxService` with methods to:
>   - List message UIDs in the intake folder.
>   - Fetch message headers by UID.
>   - Fetch full MIME content by UID as a stream or `MimeMessage`.
> - `IRoutingMailboxService` with methods to:
>   - `AppendMessageAsync` to a destination mailbox by providing a MIME stream or `MimeMessage`.
>   - It must not expose any methods to list or fetch messages from destination mailboxes.
> - `IImapClientFactory` to create configured MailKit IMAP clients.
> - A simple `ImapMessageHeader` model containing:
>   - `Uid`, `Date`, `From`, `To`, `Subject` (for in‑memory use only, not persisted).

**Prompt 2.2 – Implement MailKit services**

> In `ImapRouter.Imap`, implement:
> - `MailKitImapClientFactory` using MailKit to connect to SmarterMail with host, port, SSL, and credentials from configuration.
> - `IntakeMailboxService` that:
>   - Connects to the intake mailbox.
>   - Lists message UIDs in the configured intake folder.
>   - Fetches headers and bodies for a given UID.
> - `RoutingMailboxService` that:
>   - Connects to a destination mailbox.
>   - Performs `APPEND` of a provided MIME message into a configured destination folder.
>   - Does not implement any read/list/search operations.
> Add structured logging around all IMAP operations.

---

## Phase 3 – Persistence (metadata only)

**Prompt 3.1 – Define EF Core models**

> In `ImapRouter.Persistence`, add EF Core models:
> - `IntakeMessage` with:
>   - `Id` (GUID)
>   - `MailboxId` (string or GUID)
>   - `Uid` (long)
>   - `UidValidity` (long)
>   - `State` (enum: New, Routed, Spam, Error)
>   - `CreatedUtc` (DateTime)
>   - `UpdatedUtc` (DateTime)
> - `RoutingAction` with:
>   - `Id` (GUID)
>   - `IntakeMessageId` (FK)
>   - `ActionType` (string or enum: Route, Spam)
>   - `TargetMailboxId` (string or GUID, nullable for Spam)
>   - `PerformedByUserId` (string)
>   - `PerformedAtUtc` (DateTime)
>   - `Result` (string or enum: Success, Failed)
> Do not add any properties for subject, sender, recipient, or body.

**Prompt 3.2 – DbContext and migrations**

> Create an `ImapRouterDbContext` with `DbSet<IntakeMessage>` and `DbSet<RoutingAction>`.
> Configure it with a relational provider (e.g., SQL Server or PostgreSQL) using connection string from configuration.
> Generate an initial migration and ensure the schema matches the models with no extra columns.

---

## Phase 4 – Routing workflow

**Prompt 4.1 – Define routing service interface**

> In `ImapRouter.Core`, define `IImapRoutingService` with methods:
> - `Task RouteMessageAsync(Guid intakeMessageId, string targetMailboxId, CancellationToken ct)`
> - `Task MarkAsSpamAsync(Guid intakeMessageId, CancellationToken ct)`

**Prompt 4.2 – Implement routing logic**

> In `ImapRouter.Imap` or `ImapRouter.Persistence` (your choice, but keep it clean), implement `ImapRoutingService` that:
> - Uses `ImapRouterDbContext`, `IIntakeMailboxService`, and `IRoutingMailboxService`.
> - For `RouteMessageAsync`:
>   - Load `IntakeMessage` by Id.
>   - If `State` is not `New`, return without doing anything (idempotent).
>   - Use `IIntakeMailboxService` to fetch the full MIME message by UID.
>   - Use `IRoutingMailboxService` to `APPEND` the message to the destination mailbox.
>   - If APPEND succeeds, move the original message from the intake folder to an archive folder using IMAP MOVE (or COPY+DELETE if MOVE unsupported).
>   - Update `IntakeMessage.State` to `Routed` and save.
>   - Create a `RoutingAction` record with `Result = Success`.
>   - Log sender, recipient, subject, and date to SEQ, but do not persist them.
>   - If APPEND fails, log error, leave state as `New`, and create a `RoutingAction` with `Result = Failed`.
> - For `MarkAsSpamAsync`:
>   - Move the message from intake folder to spam/junk folder.
>   - Update state to `Spam`.
>   - Log metadata to SEQ.

---

## Phase 5 – Intake worker

**Prompt 5.1 – Define polling service**

> In `ImapRouter.Core`, define `IIntakePollingService` with a method:
> - `Task PollAsync(CancellationToken ct)`

**Prompt 5.2 – Implement polling loop**

> In `ImapRouter.Worker`, implement a background service that:
> - On a configurable interval (e.g., 10–30 seconds), calls `IIntakePollingService.PollAsync`.
> - Logs start/end of each poll and any errors.

**Prompt 5.3 – Implement PollAsync**

> In `ImapRouter.Persistence` or a dedicated service class, implement `IntakePollingService` that:
> - Uses `IIntakeMailboxService` to list all UIDs in the intake folder.
> - For each UID not present in `IntakeMessage` for that mailbox, insert a new `IntakeMessage` with:
>   - `State = New`
>   - `CreatedUtc` and `UpdatedUtc` set to now.
> - For UIDs that no longer exist in the intake folder, delete or mark the corresponding `IntakeMessage` so that it disappears from the UI.
> - Do not store any metadata beyond UID and state.

---

## Phase 6 – Auth & SSO‑ready abstraction

**Prompt 6.1 – User context abstraction**

> In `ImapRouter.Core`, define an `IUserContext` interface with:
> - `string UserId { get; }`
> - `string UserName { get; }`
> - `bool IsInRole(string role)`
> This will be used by routing services to record `PerformedByUserId`.

**Prompt 6.2 – Basic auth in Web**

> In `ImapRouter.Web`, configure ASP.NET Core authentication and authorization using:
> - Cookie auth or a simple in‑memory user for now.
> - A single role, e.g., `Router`.
> Implement an `AspNetUserContext` that implements `IUserContext` by reading from `HttpContext.User`.
> Ensure all routing endpoints require the `Router` role.

---

## Phase 7 – Web UI with MudBlazor

**Prompt 7.1 – Add MudBlazor**

> In `ImapRouter.Web`:
> - Add the MudBlazor NuGet package.
> - Configure MudBlazor in `Program.cs` and `_Imports.razor`.
> - Set up a basic layout using `MudLayout`, `MudAppBar`, and `MudDrawer`.

**Prompt 7.2 – Intake list + preview page**

> Create a Blazor page `Pages/IntakeRouter.razor` using MudBlazor components:
> - Left pane:
>   - A `MudTable` or `MudList` showing messages from `IntakeMessage` where `State = New` or `Error`.
>   - For each row, fetch headers live from IMAP using `IIntakeMailboxService` when the list is loaded or when the row becomes visible.
>   - Display From, Subject, Date in the table, but do not persist them.
> - Right pane:
>   - When a message is selected, fetch the full body from IMAP and render it in a `MudPaper` or `MudCard`.
>   - Safely render HTML content (e.g., using `MarkupString` with appropriate sanitization).

**Prompt 7.3 – Routing and Junk buttons with cancel window**

> On the right pane of `IntakeRouter.razor`, add:
> - A `MudButton` for each destination mailbox (hard‑coded list of ~15 for v1).
> - A `MudButton` for “Mark as Junk”.
> Implement the following behavior:
> - When a destination button is clicked:
>   - Do not immediately call `RouteMessageAsync`.
>   - Instead, create a “pending routing action” in component state with:
>     - `IntakeMessageId`
>     - `TargetMailboxId`
>     - `ExpiresAt` (now + configurable seconds)
>   - Show a `MudSnackbar` or `MudAlert` at the bottom:  
>     “Routing to [Destination] in X seconds… [Undo]”
>   - Start a timer that, when it expires, calls a backend API or service to execute `RouteMessageAsync`.
> - If the user clicks “Undo” before the timer expires:
>   - Cancel the pending action and do not call `RouteMessageAsync`.
> For “Mark as Junk”, you can either:
> - Execute immediately, or
> - Use the same pending/undo pattern.

**Prompt 7.4 – Backend endpoints**

> Implement minimal API endpoints or controller actions in `ImapRouter.Web` for:
> - Listing current `IntakeMessage` records (UIDs only).
> - Triggering `RouteMessageAsync`.
> - Triggering `MarkAsSpamAsync`.
> These endpoints should:
> - Use `IUserContext` to record `PerformedByUserId`.
> - Return appropriate success/failure responses for the UI.

---

## Phase 8 – Reliability & monitoring

**Prompt 8.1 – Health checks**

> Add ASP.NET Core health checks in `ImapRouter.Web` and `ImapRouter.Worker`:
> - A health check that verifies DB connectivity.
> - A health check that attempts a lightweight IMAP connection to the intake mailbox.
> Expose them at `/health`.

**Prompt 8.2 – Logging and correlation**

> Add structured logging (Serilog or built‑in) with:
> - A correlation ID per routing operation.
> - Logs for:
>   - Poll cycles.
>   - Routing attempts (start, success, failure).
>   - Spam marking.
> Ensure logs include non‑PHI identifiers (UID, mailbox, correlation ID) and may include sender/subject only in SEQ with 30‑day retention.

---

If you want, next step we can zoom into **one phase** (e.g., Phase 7 with MudBlazor) and write a “golden path” prompt plus expected file structure so you can keep Copilot on very tight rails while you implement.

> Codename first: MailMule

MailMule it is.  
That name has exactly the right energy for an internal tool: dependable, slightly stubborn, and unintentionally hilarious. And the fact that FontAwesome literally has a **donkey icon** waiting for you is cosmic confirmation that this was meant to be.

Here is a little “MailMule starter kit” you can drop straight into your repo or README to set the tone for the team.

---

# 🐴 **MailMule**  
### *“He hauls your mail so you do not have to.”*

**Icon:** `fa-donkey`  
**Vibe:** A hardworking, underappreciated creature that trudges between mailboxes with grim determination and zero questions.

---

## 🎨 MudBlazor + FontAwesome Integration Snippet

Here is a clean way to wire the icon into your MudBlazor layout:

```razor
<MudAppBar Color="Color.Primary" Elevation="4">
    <MudIcon Icon="@Icons.Filled.Mail" Size="Size.Large" Class="mr-2" />
    <MudText Typo="Typo.h5">MailMule</MudText>
    <MudSpacer />
    <MudIcon Icon="fa-solid fa-donkey" Size="Size.Large" />
</MudAppBar>
```

If you want to lean into the joke, you can even give the donkey a little animation later.

---

## 🧾 Internal Lore (Optional but delightful)

> **MailMule** is the loyal beast of burden responsible for hauling messages from the Intake Corral to their rightful pens.  
> He does not judge.  
> He does not complain.  
> He simply carries your mail across the IMAP frontier, one envelope at a time.

This kind of lore makes internal tools feel like part of the culture.

---

## 🪧 Tagline Ideas (pick your favourite)

- “MailMule: Because someone has to carry this stuff.”  
- “Routing mail the old‑fashioned way — with a mule.”  
- “One mailbox to rule them all. One mule to move them.”  
- “Hee‑haw, your mail is routed.”  
- “Powered by oats and IMAP.”  

---

## 🧱 Repo Folder Name

`/src/MailMule`  
or  
`/tools/MailMule`

Short, memorable, and fun to type.

---

## 🧩 Want a tiny ASCII badge for the README?

```
   __  _
  /  \/ )
 (     )   MailMule
  \_  /    He hauls your mail.
    ||
```

I can generate a more elaborate one if you want something banner‑worthy.

---

If you want, I can also help you:

- Add a MailMule “brand bar” to the Blazor layout  
- Create a MudBlazor theme color palette for MailMule  
- Generate a tiny SVG mascot (non‑copyrighted)  
- Write the README intro in full MailMule voice  

Just say the word and the mule saddles up.

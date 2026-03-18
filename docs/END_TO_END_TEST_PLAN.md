# End-to-End Test Plan

## Goal

Add a container-backed end-to-end test suite that validates the real MailMule mail flow against a real IMAP server and a shared SQLite database.

The first target is not browser automation. The first target is service-level confidence that the worker, routing workflow, IMAP integrations, and persistence behave correctly together under realistic conditions.

## Current Status

Implemented now:

- `Testcontainers`-backed GreenMail fixture in `MailMule.Integration.Tests`
- Temporary SQLite database per fixture
- Real worker sync coverage against IMAP and shared SQLite
- Real route, junk, undo, and partial-failure mail-flow coverage

Still pending:

- UIDVALIDITY reset coverage

The current container choice is GreenMail standalone. It was sufficient for the core flows above, but UIDVALIDITY reset behavior still needs either a GreenMail-specific reset strategy or a different IMAP test backend.

## Scope

This plan covers:

- Real IMAP mailbox interactions in tests
- Shared SQLite state used by the system under test
- Worker sync behavior
- Route, junk, undo, and partial-failure flows
- UIDVALIDITY reset handling

This plan does not initially cover:

- Browser/UI automation
- Visual validation
- Full production deployment validation

## Proposed Test Stack

- `xUnit`
- `Testcontainers`
- A real IMAP-capable test server
- Temporary SQLite database per test or per fixture

### IMAP container choice

Preferred order:

1. `GreenMail` if we need richer IMAP behavior and predictable folder/message control
2. `MailHog` only if its IMAP support is sufficient for the route, junk, and UIDVALIDITY scenarios we need

The deciding factor is behavior, not familiarity. The test server must support the operations MailMule actually uses.

Current implementation note:

- GreenMail is the active test backend for the implemented suite.

## Test Harness Design

### 1. Integration fixture

Create a reusable fixture in `MailMule.Integration.Tests` that provisions:

- one intake mailbox
- one destination mailbox
- mailbox credentials
- known folders such as `INBOX` and junk folder
- a temporary SQLite database file
- configuration values wired into the same abstractions the app uses

The fixture should expose helpers to:

- seed messages into intake
- inspect destination mailbox contents
- inspect junk folder contents
- create service scopes with the configured DI container
- run a single worker sync cycle deterministically

### 2. Shared SQLite setup

Tests should use a temporary SQLite file, not the repo database.

Each test run should override:

- connection string
- credential keys and passwords
- intake mailbox config
- destination mailbox config
- undo window seconds

The web app and worker services under test must point to the same temporary database file so behavior matches production intent.

### 3. Keep timing deterministic

Where the application uses delays or scheduling:

- shorten the undo window to 1 to 2 seconds for tests
- prefer direct invocation of queue or workflow services where possible
- avoid long sleeps
- wait on explicit conditions when possible

## Test Cases

### 1. Worker sync imports intake mail

Purpose:
Validate that the worker discovers current intake messages and projects them into SQLite.

Steps:

1. Seed one or more messages into the intake mailbox.
2. Run one sync cycle.
3. Query `IntakeMessage` rows.

Assertions:

- rows are created for the seeded UIDs
- state is `New`
- no subject, sender, or body content is persisted

### 2. Full routing path

Purpose:
Validate the main happy-path route workflow.

Steps:

1. Seed one message into intake.
2. Run one sync cycle.
3. Execute the route workflow for that message.
4. Inspect destination and intake mailboxes.
5. Inspect database state.

Assertions:

- destination mailbox contains exactly one appended copy
- intake mailbox no longer contains the message
- `IntakeMessage.State` becomes `Routed`
- a successful `RoutingAction` row is recorded

### 3. Junk path

Purpose:
Validate that junking stays within the intake mailbox.

Steps:

1. Seed one message into intake.
2. Run one sync cycle.
3. Execute junk workflow.
4. Inspect intake and junk folders.
5. Inspect database state.

Assertions:

- message is no longer in intake folder
- message exists in the configured junk folder
- `IntakeMessage.State` becomes `Junk`
- a successful junk `RoutingAction` row is recorded

### 4. Undo path

Purpose:
Validate that a pending route can be cancelled before execution.

Steps:

1. Seed one message into intake.
2. Run one sync cycle.
3. Start a pending route with a short undo window.
4. Cancel before the timer fires.
5. Inspect destination mailbox and database state.

Assertions:

- destination mailbox remains unchanged
- intake message remains available
- `IntakeMessage.State` returns to `New`
- a cancelled `RoutingAction` row is recorded

### 5. Partial failure after append

Purpose:
Validate the most important failure mode: destination append succeeds but intake removal fails.

Steps:

1. Seed one message into intake.
2. Run one sync cycle.
3. Execute route workflow while forcing `RemoveFromIntakeAsync` to fail after append succeeds.
4. Inspect destination, intake, and database state.

Assertions:

- destination mailbox contains the message
- intake mailbox still contains the message
- `IntakeMessage.State` becomes `Error`
- a failed `RoutingAction` row is recorded
- the workflow does not append a duplicate on reprocessing logic

Implementation note:

This test may require a targeted test double or wrapper around the intake removal step while keeping the rest of the IMAP path real.

### 6. UIDVALIDITY reset

Purpose:
Validate that the sync worker correctly rebuilds projection state when mailbox identity changes.

Steps:

1. Seed messages and run one sync cycle.
2. Recreate or reset the intake mailbox in a way that changes `UIDVALIDITY`.
3. Run another sync cycle.
4. Inspect database state.

Assertions:

- stale `IntakeMessage` rows are removed
- current mailbox UIDs are re-imported cleanly
- state is rebuilt from current mailbox contents only

Current status:

- Not implemented yet. The existing GreenMail-backed suite does not yet have a deterministic UIDVALIDITY reset mechanism.

## Order of Implementation

Recommended sequence:

1. Build the fixture and configuration override infrastructure.
2. Implement `Worker sync imports intake mail`.
3. Implement `Full routing path`.
4. Implement `Junk path`.
5. Implement `Undo path`.
6. Implement `Partial failure after append`.
7. Implement `UIDVALIDITY reset`.

This order establishes the core harness first, then adds higher-risk edge cases after the happy path is proven.

## Risks and Open Questions

### IMAP server suitability

We need to confirm whether the chosen container can:

- support the mailbox/folder operations MailMule uses
- allow deterministic mailbox inspection in tests
- support the UIDVALIDITY scenario or at least a workable simulation of it

### Partial failure realism

A truly real IMAP server may not make it easy to force "append succeeds, delete fails" on demand. A hybrid test may be the right tradeoff here: real destination append plus a controlled failure in the intake removal path.

### Undo timing

The current pending queue behavior may be simplest to test by reducing the configured undo window and observing state changes, but if timing becomes flaky we should introduce a more testable clock or scheduler seam.

## Non-Goals for This First Pass

These should come later if needed:

- Playwright browser coverage of the UI
- Visual assertions
- Multi-operator concurrency tests
- High-volume performance testing

## Definition of Done

This plan is considered implemented when:

- the fixture is reusable and stable
- the core happy-path routing test passes reliably
- the junk, undo, partial-failure, and UIDVALIDITY tests are in place
- tests run repeatably in local development and CI
- the suite proves that the worker and routing services operate correctly against a real IMAP backend

Current status against this definition:

- All items above are now satisfied except the UIDVALIDITY reset test.

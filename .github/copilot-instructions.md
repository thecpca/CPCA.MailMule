# CPCA.MailMule.v2 Copilot Instructions

## Build, test, and lint commands

- Build the solution with `dotnet build MailMule.slnx`.
- Run the current test suite with `dotnet test tests\CPCA.MailMule.Tests\CPCA.MailMule.Tests.csproj`.
- After a build, rerun tests faster with `dotnet test tests\CPCA.MailMule.Tests\CPCA.MailMule.Tests.csproj --no-build`.
- Build a single app with `dotnet build src\CPCA.MailMule.Backend\CPCA.MailMule.Backend.csproj`, `dotnet build src\CPCA.MailMule.ImapService\CPCA.MailMule.ImapService.csproj`, or `dotnet build src\CPCA.MailMule.Frontend\CPCA.MailMule.Frontend.csproj`.
- Run a single test with a filter, for example `dotnet test tests\CPCA.MailMule.Tests\CPCA.MailMule.Tests.csproj --filter "FullyQualifiedName~CPCA.MailMule.Tests.WebTests.HealthEndpointsReturnOkForBackendAndImapService"`.
- Run one test class with a broader filter, for example `dotnet test tests\CPCA.MailMule.Tests\CPCA.MailMule.Tests.csproj --filter "FullyQualifiedName~CPCA.MailMule.Tests.WebTests"`.
- There is no dedicated lint command checked into the repo. Formatting and style come from `.editorconfig` plus `Directory.Build.props` (`Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`).

Baseline verified in this repository: `dotnet build MailMule.slnx` succeeded, and `dotnet test tests\CPCA.MailMule.Tests\CPCA.MailMule.Tests.csproj --no-build` passed with 30 tests.

## High-level architecture

This solution is a .NET 10 / Aspire application split into three runtime apps plus shared layers:

- `src\CPCA.MailMule.Frontend` is a Blazor WebAssembly app using MudBlazor. It talks to the backend BFF over `HttpClient` and relies on the backend for auth state instead of handling OIDC directly.
- `src\CPCA.MailMule.Backend` is the BFF. It handles OpenID Connect login, issues the `bff-auth` cookie, exposes BFF endpoints like `/signin`, `/signout`, and `/bff/user`, and proxies `/api/{**catch-all}` to the IMAP service through YARP.
- `src\CPCA.MailMule.ImapService` is the IMAP-focused API. It validates JWT bearer tokens issued by the backend and owns IMAP-facing operations plus IMAP readiness checks.
- `aspire\CPCA.MailMule.AppHost` wires the distributed app together and is also the anchor for the integration-style web tests.

The shared application layering matters when making changes:

- `src\CPCA.MailMule.Application.Contracts` contains DTOs and interfaces safe to share with the frontend.
- `src\CPCA.MailMule.Application` contains service implementations and DI registrations.
- `src\CPCA.MailMule.Persistence` provides `MailMuleDbContext`, repositories, Data Protection string encryption, and the `AddMailMule(...)` registration entry point.
- `src\CPCA.MailMule.Persistence.PostgreSql` is the production database provider and migration assembly. PostgreSQL is the intended database for v2.
- `src\CPCA.MailMule.Shared` contains cross-service constants such as `MailMuleEndpoints` and `X-Correlation-ID`.

The big-picture request flow is:

1. Frontend calls the backend BFF with browser credentials included.
2. Backend authenticates the user with OIDC and cookie auth.
3. Backend forwards protected `/api/*` requests to `CPCA.MailMule.ImapService` and injects an internal JWT for service-to-service authorization.
4. Backend and IMAP service both use the same persistence/application registrations and PostgreSQL-backed data access.

## Key repository conventions

- Keep auth concerns in `Backend` and IMAP concerns in `ImapService`. `docs\REFACTOR_BFF.md` and `COPILOT.md` both establish this split: browser/OIDC/cookies/roles belong in the BFF, while IMAP UID/folder/routing behavior belongs in the IMAP service.
- Register data access with `services.AddMailMule(...)` and application services with `services.AddMailMuleApplication()`. Backend, IMAP service, and tests all follow that pattern.
- Use PostgreSQL in app code and `UseInMemoryDatabase(...)` only in tests. The current tests build service providers with in-memory EF Core and then manually seed singleton rows like `ApplicationSettings`, because the in-memory provider does not apply `HasData`.
- Preserve the frontend boundary: the Blazor WASM project references `Application.Contracts` and `Shared`, not the backend or persistence projects directly.
- `Shared\MailMuleEndpoints` is the source of truth for service names used across AppHost, tests, and inter-service communication. Reuse those constants instead of hard-coding names.
- Correlation IDs are a cross-service convention. Backend and IMAP service middleware both emit `X-Correlation-ID`, and tests assert its presence.
- Domain/shared types are intentionally explicit. `Domain.Shared` uses small value objects like `MailboxId` and `MessageId`, while domain entities such as `MailboxConfig` prefer `Create(...)` plus fluent `Set...(...)` methods instead of mutable DTO-style construction.
- Repository guidance in `COPILOT.md` is important: PostgreSQL is the target database, inter-service auth is backend-issued JWT to the IMAP service, `IStringProtector` is the intended abstraction for encrypted strings, and audit history should come from Serilog/SEQ rather than a separate routing-action audit table.
- Message routing behavior is constrained by the product docs: routing is a move-like workflow, `MailboxConfig.DeleteMessage` controls delete-vs-archive behavior for the source mailbox, and the archive path must exist when messages are retained.
- Tests are mixed unit/integration coverage inside `tests\CPCA.MailMule.Tests`. `WebTests` use `DistributedApplicationTestingBuilder<Projects.CPCA_MailMule_AppHost>` to boot the Aspire app graph, while service tests usually compose a `ServiceCollection` directly.

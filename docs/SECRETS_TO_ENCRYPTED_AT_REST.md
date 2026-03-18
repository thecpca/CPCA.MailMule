**Implementation Plan**

1. Introduce encrypted password fields in the database model

- Replace CredentialKey with EncryptedPassword on intake and destination config entities.
- Suggested constraints: required, max length around 4000 (Data Protection payload is much longer than plaintext).
- Files:
  - IntakeMailboxConfig.cs
  - DestinationMailboxConfig.cs
  - MailMuleDbContext.cs

2. Create a migration with no compatibility path

- Since no backward compatibility is required, do a direct schema change:
  - drop CredentialKey columns
  - add EncryptedPassword columns (NOT NULL)
- Update snapshot.
- Files:
  - Migrations
- Note: if existing dev DBs exist locally, easiest is recreate DB after migration update.

3. Replace resolver abstraction with crypto abstraction

- Remove key-based resolver pattern.
- Add interface in Core, for example:
  - Protect plaintext
  - Unprotect ciphertext
- Implement with IDataProtectionProvider in Persistence.
- Files:
  - ICredentialResolver.cs (replace or delete)
  - Services
- Purpose string should be stable and explicit, for example MailMule.ImapPassword.v1.

4. Configure Data Protection key ring for Docker

- Configure both Web and Worker identically:
  - PersistKeysToFileSystem to a bind-mounted directory
  - SetApplicationName same value in both processes
  - Protect keys with a certificate loaded from mounted file, password from environment variable
- This matches your requirement: key material from bind mount, key password from environment variable.
- Files:
  - Program.cs
  - Program.cs
- Add config options in appsettings for:
  - DataProtection:KeysPath
  - DataProtection:CertificatePath
  - DataProtection:CertificatePasswordEnvVar
  - optional DataProtection:ApplicationName

5. Refactor admin save/test workflows to accept plaintext password input

- On save:
  - if password field provided, encrypt and persist to EncryptedPassword
  - if editing and blank password submitted, preserve existing encrypted value
- On test connection:
  - decrypt persisted EncryptedPassword and authenticate
- Files:
  - AdminConfigurationService.cs
  - IAdminConfigurationService.cs
- Important: avoid ever returning encrypted value to UI model.

6. Update IMAP client factory to decrypt persisted password

- Change calls from Resolve(credentialKey) to Unprotect(encryptedPassword).
- Keep connection logic unchanged.
- File:
  - MailKitImapClientFactory.cs

7. Update admin UI from Credential Key to Password semantics

- Intake page:
  - replace Credential Key textbox with password input
  - add helper text for update behavior (leave blank to keep existing password)
- Destination dialog:
  - same behavior
- File targets:
  - Intake.razor
  - DestinationEditDialog.razor
  - Destinations.razor
- Recommended: use edit DTO/view-models instead of binding EF entities directly, so plaintext password is transient only.

8. Test updates

- Remove/replace configuration resolver tests:
  - ConfigurationCredentialResolverTests.cs
- Add crypto service unit tests:
  - roundtrip protect/unprotect
  - invalid ciphertext fails predictably
- Update IMAP factory tests to seed encrypted values and use fake unprotector:
  - MailKitImapClientFactoryTests.cs
- Update integration helpers that currently seed CredentialKey:
  - TestHelpers.cs

9. Documentation updates

- Replace all user-secrets credential-key guidance with encrypted-in-DB guidance.
- Document required container mounts/env vars for Data Protection.
- Files:
  - README.md
  - IMPLEMENTATION_PLAN.md

**Operational design recommendations**

- Use one shared mounted key directory for both Web and Worker so either process can decrypt.
- Use certificate-based key protection for Linux containers:
  - mount .pfx read-only
  - provide password via env var
- Backup the Data Protection key ring and certificate together for disaster recovery.
- Never log plaintext password, encrypted payload, or decryption exceptions with payload content.

**Execution order (low-risk path)**

1. Add crypto abstraction + Data Protection config
2. Add entity/schema changes + migration
3. Update admin service and IMAP factory
4. Update UI models/pages
5. Update tests
6. Update docs
7. Run full test suite and manual admin flow verification

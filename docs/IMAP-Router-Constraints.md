# MailMule — IMAP Router Technical Constraints

This document is the authoritative reference for the non-negotiable rules that govern how MailMule
interacts with email, IMAP servers, and persistent storage. Every implementation decision must be
consistent with these constraints.

---

## 1. Privacy Constraints

- All message headers and bodies are fetched live from the incoming IMAP mailboxes on demand and held only in process memory for the duration of the operations (display or routing). They MUST NOT be written to disk, a database, or a distributed cache.
- No email content or metadata — subject, sender, recipient, body, headers, or attachments — is stored in any database, cache, or file on disk.
- Structured Logs (Serilog -> SEQ) may temporarily contain sender address, recipient address, subject, and date solely for operational observability. This app is not responsible for Log Retention or expiry.
- This posture is consistent with PIPEDA, PIPA, and HIPAA minimum-necessary principles.

---

## 2. Routing Semantics

- Routing a message means delivering the **original, unmodified MimeMessage** to the destination mailbox and permanently removing it from the incoming mailbox.
- The operation is always:
  1. `APPEND` the original MimeMessage to the destination mailbox.
  2. Only after a successful `APPEND` response: mark the source message `\Deleted` in the incoming mailbox and `EXPUNGE`.
- No retained copy of the message remains anywhere in the incoming IMAP account after successful routing.
- Cross-account IMAP `MOVE` does not exist as a protocol operation. The two-step `APPEND` + `DELETE/EXPUNGE` described above **is** the implementation of "move to destination".
- The original message is never rewritten, re-encoded, or header-modified. Headers such as `From`, `To`, `Message-ID`, and `Reply-To` are preserved exactly. This ensures that when the recipient clicks Reply, their mail client addresses the original sender, not the incoming mailbox.
- If Step 1 (`APPEND`) fails, Step 2 (`DELETE/EXPUNGE`) must not execute. The message remains in incoming unchanged.
- If Step 1 (`APPEND`) succeeds but Step 2 (`DELETE/EXPUNGE`) fails, the message exists in both the destination mailbox and the incoming mailbox. This is a partial-failure condition. The routing record is marked `Error`. The operator must resolve it via the admin error queue. **MailMule must not automatically retry the `APPEND`**, as doing so would create a duplicate in the destination.

---

## 3. Destination Mailboxes are Write-Only

- Destination mailboxes are treated as **append-only black holes** by MailMule.
- The only permitted IMAP operation against any destination mailbox is `APPEND`.
- MailMule must **never** list, search, fetch, flag, copy, move, or inspect messages in destination mailboxes for any reason.
- Success is determined solely by a successful `APPEND` response from the IMAP server. MailMule does not verify delivery by re-reading the destination.

---

## 4. Junk Handling

- Marking a message as junk is an IMAP `MOVE` entirely within the incoming IMAP account.
- The message is moved from the configured incoming folder (e.g., `INBOX`) to the configured junk folder (e.g., `Junk E-mail`) on the same IMAP server and account.
- MailMule does not delete messages when marking junk. It only relocates them.
- If the IMAP `MOVE` extension is unavailable, the fallback is `COPY` to the junk folder followed by `DELETE/EXPUNGE` from the incoming folder.
- Destination mailbox rules do not apply to junk handling; both source and target folders reside within the incoming account.

---

## 5. Incoming Sync

- The operator's "inbox" always reflects the aggregated **current, real-time contents** of the incoming IMAP folders.
- Sync is maintained by a background polling service. Each incoming mailbox has it's own Poll Interval which must be respected
  - IMAP `IDLE` should be used to reduce the frequency of reconnecting/authenticating.
  - Minimum Poll Interval is 15 seconds. Default is 300 seconds.
- On each poll cycle:
  - UIDs present in IMAP but absent from the local projection are inserted with `State = New`.
  - UIDs absent from IMAP but present in the local projection with `State = New` or `Error` are removed from the local projection without recording why.
  - UIDs with `State = Routing`, `Routed`, or `Junk` are not removed during a disappearance check.
- **UIDVALIDITY changes** indicate the mailbox was deleted and recreated or renamed. When detected:
  - All local projection records for that mailbox are deleted.
  - All current IMAP UIDs are re-imported as `State = New`.
  - Prior routing history (`RoutingAction` records) is preserved; only the active projection is cleared.
- Poll interval is configurable per incoming mailbox (default: 20 seconds).
- If the IMAP connection fails during a poll cycle, the error is logged and the cycle is skipped. The next cycle retries from the beginning.

---

## 6. SmarterMail Notes

> **Note:** The items below require validation against the production SmarterMail instance before
> implementation begins. Update this section with confirmed values before starting Phase 3.

### Folder Names

| Purpose        | Expected Folder Name | Confirmed |
|----------------|----------------------|-----------|
| Incoming inbox | `INBOX`              | [ ]       |
| Junk / Spam    | `Junk E-mail`        | [ ]       |

To confirm the exact junk folder name, connect an IMAP client (e.g., Thunderbird or the MailKit test console) to the incoming account and list all folder names.

### IMAP Extension Availability

| Extension | Purpose                              | Required | Available | Notes |
|-----------|--------------------------------------|----------|-----------|-------|
| `UIDPLUS` | Reliable UID assignment on APPEND    | Yes      | [ ]       |       |
| `MOVE`    | Atomic single-folder move (junk)     | No       | [ ]       | Fallback: COPY + EXPUNGE |
| `IDLE`    | Push notification instead of polling | No       | [ ]       | Not used in v1; vNext candidate |

To check available extensions, connect with MailKit and inspect `client.Capabilities` after authentication. Log the full capabilities string during Phase 3 implementation.

### Known SmarterMail Behaviours

- SmarterMail supports IMAP UIDPLUS; UIDs are stable within a UIDVALIDITY epoch.
- SmarterMail may reset UIDVALIDITY when a folder is deleted and recreated.
- The `MOVE` extension availability depends on the SmarterMail version and configuration; always implement the COPY+EXPUNGE fallback.

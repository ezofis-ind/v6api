# Email AP Agent + Connector Hub — UI & Service Handoff

**Auth:** `Authorization: Bearer <JWT>` + `X-Tenant-Id: <tenant-guid>`

## Architecture

| Piece | Role |
|-------|------|
| `catalog.ConnectorProviders` | Global OAuth apps (GMAIL, OUTLOOK, QUICKBOOKS, …) |
| `dbo.connector` | Per-tenant OAuth tokens |
| `dbo.EmailIngestMailbox` | Which mailbox → which AP workflow + master source |
| `dbo.EmailIngestProcessed` | Dedup (message + attachment already started) |
| Hangfire `email-ingest-poll` | Every minute; respects each mailbox `pollIntervalMinutes` |
| Existing AP Agent | Multipart workflow start still enqueues Python AP Agent |

## UI setup

1. Connect Gmail and/or Outlook: `POST /api/connector/oauth/authorize` (`providerCode`: `GMAIL` / `OUTLOOK`).
2. Optionally connect QuickBooks for masters.
3. **Preferred:** create/update an EMAIL workflow with `emailConnectorId` (auto-creates `EmailIngestMailbox`):

```http
POST /api/workflows
{
  "name": "AP Email Invoice",
  "triggerType": 0,
  "publishImmediately": true,
  "emailConnectorId": "<gmail-or-outlook-connector-guid>",
  "emailIsEnabled": true,
  "emailPollIntervalMinutes": 5,
  "emailQueryFilter": "has:attachment subject:invoice",
  "masterSource": "InternalForm",
  "masterFormId": "<vendor-form-id>",
  "workflowJson": {
    "Settings": {
      "General": {
        "Name": "AP Email Invoice",
        "InitiateUsing": { "Type": "EMAIL" }
      },
      "Publish": { "PublishOption": "PUBLISHED" }
    },
    "Blocks": [
      {
        "Id": "start-1",
        "Type": "START",
        "Settings": {
          "InitiateBy": ["EMAIL"],
          "MailInitiate": { "ConnectorId": "<same-guid-optional>" }
        }
      }
    ]
  }
}
```

Response includes `emailIngestMailboxId`, `emailConnectorId`, `emailIngestEnabled`.  
`PUT /api/workflows/{id}` accepts the same email fields (returns 200 with those fields).  
`GET /api/workflows/{id}` also returns the linked mailbox fields.

Legacy int `MailInitiate.ConnectorId` is rejected — use OAuth Guid only.

4. **Alternative:** create mailbox manually:

```http
POST /api/email-ingest/mailboxes
{
  "connectorId": "<gmail-or-outlook-connector-guid>",
  "workflowId": "<ap-workflow-guid>",
  "isEnabled": true,
  "pollIntervalMinutes": 5,
  "queryFilter": "has:attachment subject:invoice",
  "masterSource": "InternalForm",
  "masterFormId": "<vendor-form-id>",
  "attachmentExtensions": ".pdf,.tif,.tiff"
}
```

For QuickBooks masters use `"masterSource": "QuickBooks"` + `"masterConnectorId": "<qbo-connector-guid>"`.

5. Manual test: `POST /api/email-ingest/mailboxes/{id}/poll`
6. List/status: `GET /api/email-ingest/mailboxes`

## Mail ops (Gmail + Outlook, same routes)

| Method | Path |
|--------|------|
| GET | `/api/connector/{id}/mail/summary` → `{ totalCount, unreadCount }` |
| GET | `/api/connector/{id}/mail/messages?unreadOnly=&maxResults=&query=` |
| GET | `/api/connector/{id}/mail/messages/top?unreadOnly=true` |
| GET | `/api/connector/{id}/mail/messages/{messageId}` |
| POST | `/api/connector/{id}/mail/messages/{messageId}/read` |
| GET | `/api/connector/{id}/mail/messages/{messageId}/attachments/{attachmentId}` |

**Re-authorize required** after scope bump: Gmail `gmail.modify`, Outlook `Mail.ReadWrite`.

## Master resolve (UI + Python)

```http
GET /api/master/resolve?type=Vendor|Customer|Item&q=&maxResults=50&mailboxId=
GET /api/master/resolve?type=Vendor&source=InternalForm&formId=...
GET /api/master/resolve?type=Vendor&source=QuickBooks&connectorId=...
```

Response items: `{ id, type, displayName, email, source, externalId, raw }`.

Do **not** store Google/Microsoft/Intuit tokens in Python — call V6 with tenant JWT.

## Poller behavior

1. List unread INBOX (optional `queryFilter`).
2. For matching invoice attachments → `StartWorkflow` with file + `TriggerApAgentPythonJob`.
3. Context JSON includes `messageId`, `from`, `subject`, `masterSource`, etc.
4. Dedup in `EmailIngestProcessed`; mark message read only if at least one attachment started.
5. Emails with no matching attachment stay unread.

## QuickBooks × AP Agent (integration path)

**Phase 1 — Master match (current; no Intuit tokens in Python)**

1. Connect QuickBooks: `POST /api/connector/oauth/authorize` with `providerCode: "QUICKBOOKS"`.
2. Create/update EMAIL workflow with `masterSource: "QuickBooks"` and `masterConnectorId: "<qbo-guid>"`.
3. Email ingest passes `masterSource` / `masterConnectorId` in workflow start `Context` (see `EmailIngestService`).
4. After OCR, Python AP Agent calls V6 only:

```http
GET /api/master/resolve?type=Vendor&source=QuickBooks&connectorId=<qbo-guid>&q=<extractedVendor>&maxResults=20
Authorization: Bearer <JWT>
X-Tenant-Id: <tenant-guid>
```

5. Auto-select vendor when one strong match; otherwise return candidates for UI review.

**Phase 2 — Post Bill to QuickBooks (planned)**

- New V6 endpoint: `POST /api/connector/{id}/quickbooks/bills` (VendorRef, lines, DocNumber).
- Trigger on workflow approve/move-next when workflow config enables `postToQuickBooks`.
- Python/UI still use JWT; never store QBO refresh tokens outside V6.

**Phase 3 — Pull QBO documents as intake (later)**

- Poll `GET /api/connector/{id}/quickbooks/documents` similar to email ingest.

## SQL

- Tenant tables: `scripts/Create-EmailIngest-Tables.sql` (also auto-created on first API use).
- Catalog scopes: re-run seed / `CreateCatalogConnectorProviders.sql` MERGE updates GMAIL/OUTLOOK scopes.

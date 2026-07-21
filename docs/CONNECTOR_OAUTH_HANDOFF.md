# Connector OAuth — Frontend & Service Handoff

**Audience:** Frontend, Python/other services  
**Auth (most routes):** `Authorization: Bearer <JWT>` + `X-Tenant-Id: <tenant-guid>`  
**Callback:** anonymous (validated by signed `state`)

---

## Architecture (short)

| Store | What |
|-------|------|
| **Catalog DB** `catalog.ConnectorProviders` | Global OAuth apps (ClientId, Secret, Auth/Token URLs, Scopes, RedirectUri) |
| **Tenant DB** `dbo.connector` | Per-tenant connection + `accessToken` / `refreshToken` / expiry / status |

Providers in Phase 1: `GCP`, `GMAIL`, `ONEDRIVE`, `TEAMS`, `DROPBOX`.  
Adding more later = new Catalog row + adapter (same APIs).

---

## Setup (ops)

1. Run `scripts/CreateCatalogConnectorProviders.sql` on the **catalog** database (or let the API auto-create/seed on first `GET /api/connector/providers`).
2. Update each provider row with real secrets (do **not** commit secrets):

```sql
UPDATE catalog.ConnectorProviders
SET ClientId = N'...',
    ClientSecret = N'...',
    RedirectUri = N'https://your-host/V6API/api/connector/oauth/callback'
WHERE ProviderCode = N'GCP';
-- Repeat for GMAIL, ONEDRIVE, TEAMS, DROPBOX (Google apps can share ClientId; Microsoft apps can share ClientId).
```

3. Register the **same RedirectUri** in Google Cloud Console / Azure App Registration / Dropbox App Console.
4. Optional `appsettings`:

```json
"ConnectorOAuth": {
  "StateSigningKey": "",
  "StateTtlMinutes": 15,
  "RefreshSkewMinutes": 5,
  "DefaultSuccessRedirectUrl": "https://your-ui/connectors/oauth-complete"
}
```

`StateSigningKey` falls back to `EzofisAuth:SigningKey` when empty.

5. Tenant OAuth columns are auto-added on first OAuth use; or run `scripts/AddConnectorOAuthTokenColumns.sql`.

---

## UI connect flow

```
1. GET  /api/connector/providers
2. POST /api/connector/oauth/authorize
      { "providerCode": "GCP", "name": "My GCS", "configJson": "{\"bucket\":\"my-bucket\"}",
        "successRedirectUrl": "https://ui/connectors/oauth-complete" }
   → { connectorId, authorizationUrl, state }
3. window.location = authorizationUrl  (or popup)
4. Provider redirects → GET /api/connector/oauth/callback?code=...&state=...
5. API exchanges code, saves tokens on tenant connector, redirects to successRedirectUrl
      ?connectorOAuth=success&connectorId=...&provider=GCP
6. GET /api/connector/{connectorId}/status  → isConnected, externalAccountEmail, expiry
```

**GCP:** pass `configJson` with at least `{ "bucket": "<gcs-bucket-name>" }` before or during authorize.  
**Gmail / OneDrive / Teams / Dropbox:** `configJson` optional for Phase 1.

Re-auth existing connector: send `connectorId` in authorize body.

Disconnect: `POST /api/connector/{id}/disconnect`

Authorize body example:

```json
{
  "providerCode": "GCP",
  "name": "My GCS",
  "configJson": "{\"bucket\":\"my-bucket\"}",
  "successRedirectUrl": "https://ui/connectors/oauth-complete"
}
```

---

## Endpoints

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| `GET` | `/api/connector/providers` | JWT | List providers (no secrets) |
| `POST` | `/api/connector/oauth/authorize` | JWT | Start OAuth |
| `GET` | `/api/connector/oauth/callback` | Anonymous | Provider redirect |
| `POST` | `/api/connector/{id}/oauth/refresh` | JWT | Refresh token |
| `POST` | `/api/connector/{id}/disconnect` | JWT | Clear tokens |
| `GET` | `/api/connector/{id}/status` | JWT | Connection status |
| `GET` | `/api/connector/{id}/files?path=` | JWT | List files |
| `POST` | `/api/connector/{id}/files/upload` | JWT | Multipart `file` + optional `path` |
| `GET` | `/api/connector/{id}/files/download?path=` | JWT | Download file |
| `GET` | `/api/connector/{id}/gmail/messages?maxResults=&query=` | JWT | Gmail list |
| `GET` | `/api/connector/{id}/gmail/messages/{messageId}/attachments/{attachmentId}` | JWT | Download attachment |

Existing CRUD (`POST/PUT/GET /api/connector`, `/all`) is unchanged. **Access/refresh tokens are never returned** in connector DTOs.

---

## Other services (Python / Hangfire)

1. Resolve tenant + connector id.
2. Call V6 with tenant JWT (or service account) and use file/Gmail endpoints above.
3. Or share the same Catalog + tenant token columns and refresh via `POST .../oauth/refresh` when `tokenExpiresAtUtc` is near.

Tokens auto-refresh on file/Gmail ops when expiry is within `RefreshSkewMinutes`.

---

## Provider capability matrix

| Code | Files | Mail | QuickBooks |
|------|-------|------|------------|
| GCP | Yes | No | No |
| GMAIL | No | Yes | No |
| OUTLOOK | No | Yes (Office 365) | No |
| ONEDRIVE | Yes | No | No |
| TEAMS | Yes (Graph drive) | No | No |
| DROPBOX | Yes | No | No |
| QUICKBOOKS | No | No | Masters + documents (+ PDF); realmId on callback |

Mail list/download (GMAIL + OUTLOOK):

- `GET /api/connector/{id}/mail/summary`
- `GET /api/connector/{id}/mail/messages?unreadOnly=&maxResults=&query=`
- `GET /api/connector/{id}/mail/messages/top`
- `GET /api/connector/{id}/mail/messages/{messageId}`
- `POST /api/connector/{id}/mail/messages/{messageId}/read` (requires `gmail.modify` / `Mail.ReadWrite` — re-authorize)
- `GET /api/connector/{id}/mail/messages/{messageId}/attachments/{attachmentId}`

Email → AP Agent ingest: see [EMAIL_AP_AGENT_HANDOFF.md](EMAIL_AP_AGENT_HANDOFF.md) (`/api/email-ingest/...`, Hangfire poller, `/api/master/resolve`).

QuickBooks (provider code `QUICKBOOKS`):

- `GET /api/connector/{id}/quickbooks/masters?masterType=Customer|Vendor|Item`
- `GET /api/connector/{id}/quickbooks/documents?documentType=Invoice|Bill|PurchaseOrder|Estimate|SalesReceipt`
- `GET /api/connector/{id}/quickbooks/documents/{documentId}/pdf?documentType=Invoice`
- Sandbox: set connector `configJson` to `{"environment":"sandbox"}`

**Smoke test script:** `scripts/Test-QuickBooksConnector.ps1` (login + status + masters + documents + `master/resolve`). Requires SQL (`EZOFIS_DELL_I9` or your server) and API on `:5000`.

```powershell
.\scripts\Test-QuickBooksConnector.ps1 `
  -ApiBase http://localhost:5000 `
  -Email you@company.com `
  -Password '***' `
  -TenantId 0B3E1B77-4A6C-46F2-83EE-2F0A5B84956B `
  -SqlServer EZOFIS_DELL_I9 `
  -SqlPassword '***'
```

AP Agent × QuickBooks integration path: see [EMAIL_AP_AGENT_HANDOFF.md](EMAIL_AP_AGENT_HANDOFF.md#quickbooks--ap-agent-integration-path).

---

## Errors

- `Provider 'X' is not configured` → Catalog ClientId/Secret/RedirectUri empty  
- `Connector is not connected` → user must complete OAuth  
- `does not support file/Gmail/QuickBooks operations` → wrong provider for that endpoint  
- `QuickBooks realmId is missing` → disconnect and re-authorize so callback stores `realmId`
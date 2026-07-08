# V6 API Handoff — Repository Share, Workflow Share, Credits

**To:** Frontend / Integration team  
**From:** Backend team  
**Date:** July 2026  

Use this as the single reference for the four features below.  
Full detail: `docs/TEAM_API_GUIDE_CREDITS_AND_WORKFLOW_SHARE.md`

---

## Common setup (all APIs)

```
Base URL:     https://your-host/V6API   (local: https://localhost:44311)
Authorization: Bearer <JWT>
X-Tenant-Id:  <tenant-guid>
Content-Type: application/json   (POST bodies)
```

---

## 1. Repository file share (cross-tenant, read-only)

Share an archived repository file by email. Recipient logs in and opens the **same file view page** with an optional `shareToken`.

### Create share

```http
POST /api/repositories/{repositoryId}/items/{itemId}/share
Authorization: Bearer <jwt>
X-Tenant-Id: <tenant-guid>
```

**Request:**
```json
{
  "email": "recipient@example.com",
  "message": "Please review this document"
}
```

**Response `201`:**
```json
{
  "shareId": "guid",
  "shareToken": "abc123...",
  "sourceRepositoryId": "repo-guid",
  "sourceItemId": "item-guid",
  "recipientEmail": "recipient@example.com",
  "expiresAtUtc": "2026-08-07T10:00:00Z",
  "shareUrl": "https://demoapp.ezofis.com/sign-in?shareToken=abc123...&email=recipient%40example.com"
}
```

### Other repository share APIs

| Action | Method | Endpoint | Auth |
|--------|--------|----------|------|
| List shared with me | GET | `/api/repositories/shared-with-me` | JWT + tenant |
| Preview before login | GET | `/api/repositories/share/{shareToken}/preview` | Anonymous |
| Revoke share | DELETE | `/api/repositories/share/{shareId}` | JWT + tenant |

### View shared file (reuse existing APIs + token)

```http
GET /api/repositories/{repoId}/items/{itemId}/workspace?sharedtoken={shareToken}
GET /api/repositories/{repoId}/items/{itemId}/file?sharedtoken={shareToken}&disposition=inline
Authorization: Bearer <jwt>
X-Tenant-Id: <tenant-guid>
```

Also works: query `shareToken` or header `X-Share-Token`.  
Writes (upload/edit/comment POST) return `403` on shared files.

---

## 2. Workflow inbox file share (verify concept)

Share a workflow inbox document with an external verifier. Creates guest user, assigns inbox task, and returns a share link.

### Create workflow share

```http
POST /api/workflows/instances/{instanceId}/share-file
Authorization: Bearer <jwt>
X-Tenant-Id: <tenant-guid>
```

**Request:**
```json
{
  "email": "verifier@external.com",
  "repositoryId": "repo-guid",
  "itemId": "item-guid",
  "message": "Please verify this invoice"
}
```

**Response `201`:**
```json
{
  "share": {
    "shareId": "guid",
    "shareToken": "abc123...",
    "shareUrl": "https://demoapp.ezofis.com/sign-in?shareToken=...&email=...",
    "sourceRepositoryId": "repo-guid",
    "sourceItemId": "item-guid"
  },
  "inboxAssignment": {
    "workflowId": "workflow-guid",
    "workflowInstanceId": "instance-guid",
    "transactionId": 42,
    "activityId": "VERIFY_STEP",
    "guestUserId": "guest-guid",
    "inboxAssigned": true
  },
  "guestUserId": "guest-guid"
}
```

### Recipient login flow (from email link)

**Step 1 — Preview** (decides which sign-in UI to show):
```http
GET /api/repositories/share/{shareToken}/preview
```

**New guest (first visit)** — show all three options:
```json
{
  "allowedAuthMethods": ["password_setup", "google", "microsoft"],
  "requiredSocialProvider": null,
  "requiresPasswordSetup": true,
  "sourceTenantId": "tenant-guid",
  "workflowInstanceId": "instance-guid"
}
```

**Return visit** — preview returns only the method they chose before (`google`, `microsoft`, or `password_login`).

**Step 2a — Set password** (EZOFIS guest):
```http
POST /api/auth/share/set-password
```
```json
{ "shareToken": "...", "email": "...", "password": "..." }
```

**Step 2b — Social login** (after Google/Microsoft OAuth on client):
```http
POST /api/auth/share/social-login
```
```json
{ "shareToken": "...", "email": "...", "provider": "google" }
```

**Step 3 — View file** (same as repository share + `sharedtoken`)

**Step 4 — Verify / approve:**
```http
POST /api/workflows/instances/{instanceId}/move-next
X-Tenant-Id: <sourceTenantId from preview>
```
```json
{
  "activityId": "VERIFY_STEP",
  "review": "Approve",
  "comments": "Verified"
}
```

**Return visit login:**
```http
POST /api/auth/ezofis/login          (password users)
POST /api/auth/social/login          (Google/Microsoft users)
X-Tenant-Id: <tenant-guid>
```

---

## 3. Credit insert (consume credits)

Called when a billable action runs (AP Agent, OCR, Document Summary, etc.).  
Writes to `dbo.creditMaster` (balance) and `dbo.creditTransaction_MMyy` (ledger).

```http
POST /api/billing/credits/update
Authorization: Bearer <jwt>
X-Tenant-Id: <tenant-guid>
```

**Request:**
```json
{
  "activityType": "OCR Agent",
  "subActivity": "AI OCR",
  "identify": "Document",
  "identifyId": 101,
  "remarks": "Invoice extraction",
  "credit": 1,
  "env": "live",
  "inputTokens": 1200,
  "outputTokens": 300,
  "totalTokens": 1500
}
```

**Response:**
```json
{ "id": 1, "output": "credits updated" }
```

| `id` | Meaning |
|------|---------|
| `1` | Success |
| `2` | Credit limit exceeded |
| `0` | Failed (no creditMaster row for this month) |

**Get current balance:**
```http
GET /api/billing/credits/master?allocationMonth=7&allocationYear=2026
```

**DB setup:** Run `src/Api/scripts/AddCreditMaster.sql` on catalog DB.  
New tenants get default credits automatically on signup. Existing tenants: script backfills from `catalog.Tenants`.

---

## 4. Credit usage dashboard (single POST API)

One API for all dashboard widgets (tables, pie chart, trend, monthly list).

```http
POST /api/billing/credits/usage
Authorization: Bearer <jwt>
X-Tenant-Id: <tenant-guid>
```

**Request examples:**
```json
{ "period": "today" }
{ "period": "yesterday" }
{ "period": "monthly" }
{ "period": "monthly", "year": 2026, "month": 6 }
{ "period": "quarterly" }
{ "period": "yearly" }
```

**Response fields → UI widgets:**

| UI widget | Response field |
|-----------|----------------|
| Highest Credit Consumption (bar table) | `highestConsumption` |
| Credit Distribution Report (detail table) | `distributionReport` |
| Overall Credit Split (pie chart) | `overallCreditSplit` |
| Credit trend (line chart) | `timeline` |
| Monthly list (yearly/quarterly) | `monthlyConsumption` |
| Transaction drill-down | `transactions` |
| Grand total | `totalCreditsConsumed` |

**Sample response (monthly):**
```json
{
  "period": "Monthly",
  "periodLabel": "June 2026",
  "totalCreditsConsumed": 12580,
  "highestConsumption": [
    { "type": "AP Agent", "creditsUsed": 4850 },
    { "type": "OCR Agent", "creditsUsed": 3920 },
    { "type": "Total", "creditsUsed": 12580 }
  ],
  "distributionReport": [
    { "type": "Invoice OCR Extraction", "creditsUsed": 3120 },
    { "type": "PO Line Matching", "creditsUsed": 2260 }
  ],
  "overallCreditSplit": [
    { "type": "AP Agent", "creditsUsed": 4850 },
    { "type": "OCR Agent", "creditsUsed": 3920 },
    { "type": "Supplier Validation", "creditsUsed": 980 }
  ],
  "timeline": [
    { "label": "Week 1", "creditsUsed": 2100 },
    { "label": "Week 2", "creditsUsed": 2850 }
  ],
  "monthlyConsumption": null,
  "transactions": [ ... ]
}
```

**Recommended `activityType` values** (for correct chart grouping when calling credit update):
- `AP Agent`, `OCR Agent`, `Document Summary`
- Use `remarks` / `subActivity` with keywords `supplier`, `duplicate`, `back order` for pie chart sub-categories

---

## Quick reference — all endpoints

| Feature | Method | Endpoint |
|---------|--------|----------|
| **Repository share** | POST | `/api/repositories/{repoId}/items/{itemId}/share` |
| Shared with me | GET | `/api/repositories/shared-with-me` |
| Share preview | GET | `/api/repositories/share/{shareToken}/preview` |
| View shared file | GET | `/api/repositories/{repoId}/items/{itemId}/file?sharedtoken=...` |
| **Workflow share** | POST | `/api/workflows/instances/{instanceId}/share-file` |
| Guest set password | POST | `/api/auth/share/set-password` |
| Guest social login | POST | `/api/auth/share/social-login` |
| Verify / approve | POST | `/api/workflows/instances/{instanceId}/move-next` |
| **Credit insert** | POST | `/api/billing/credits/update` |
| Credit balance | GET | `/api/billing/credits/master` |
| **Credit usage** | POST | `/api/billing/credits/usage` |

---

## Deployment checklist

- [ ] Catalog DB: run `src/Api/scripts/AddCreditMaster.sql`
- [ ] `RepositoryShare:FrontendBaseUrl` in appsettings (share email links)
- [ ] `TenantDefaultCredit` in appsettings (default credits on new tenant signup)
- [ ] Mail settings in catalog for share notification emails

---

## Support docs in repo

| File | Contents |
|------|----------|
| `docs/TEAM_EMAIL_V6_API_HANDOFF.md` | This document (email handoff) |
| `docs/TEAM_API_GUIDE_CREDITS_AND_WORKFLOW_SHARE.md` | Full detail + flows + checklists |
| `docs/FRONTEND_TEAM_API_GUIDE.md` | Repository share + inbox/sent + AP Agent |

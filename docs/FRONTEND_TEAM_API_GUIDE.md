# V6 API — Frontend Integration Guide (Recent Updates)

**Audience:** Frontend team  
**Merged in:** PR #7 (`main`)  
**Base URL example:** `https://localhost:44311` or `https://your-host/V6API`  
**Auth (most routes):** `Authorization: Bearer <JWT>` + `X-Tenant-Id: <tenant-guid>`

---

## Table of contents

1. [Repository file sharing (cross-tenant)](#1-repository-file-sharing-cross-tenant)
2. [Inbox / Sent fix (completed)](#2-inbox--sent-fix-completed)
3. [Hangfire performance (backend)](#3-hangfire-performance-backend)
4. [AP Agent — multiple job status fetch](#4-ap-agent--multiple-job-status-fetch)
5. [Bulk move-next](#5-bulk-move-next)

---

## 1. Repository file sharing (cross-tenant)

Share an archived repository file by email. The recipient signs up or logs in, then opens the **same file view page** using existing repository APIs plus an optional share token. Shared access is **read-only**.

### 1.1 Flow overview

```
Sharer (Tenant A)                    Recipient (Tenant B)
      |                                      |
      | POST .../items/{itemId}/share        |
      | { email, message }                   |
      |<-- shareToken, shareUrl -------------|
      |                                      |
      | email link ------------------------->| sign-in?shareToken=...&email=...
      |                                      | login / signup
      |                                      | GET shared-with-me (optional)
      |                                      | GET item/workspace/... ?sharedtoken=...
```

**First visit (from email link):** read `shareToken` and `email` from the sign-in URL query string.  
**Return visit (normal login):** call `GET /api/repositories/shared-with-me` to get active shares and their tokens.

### 1.2 New / updated endpoints

| Action | Method | Endpoint | Auth |
|--------|--------|----------|------|
| Create share + send email | `POST` | `/api/repositories/{repositoryId}/items/{itemId}/share` | JWT + tenant |
| List files shared with me | `GET` | `/api/repositories/shared-with-me` | JWT + tenant |
| Preview before login | `GET` | `/api/repositories/share/{shareToken}/preview` | **Anonymous** |
| Revoke share | `DELETE` | `/api/repositories/share/{shareId}` | JWT + tenant |

**Existing item routes** (unchanged paths; add optional share token):

| Action | Method | Endpoint |
|--------|--------|----------|
| Get item | `GET` | `/api/repositories/{id}/items/{itemId}` |
| Workspace (detail view) | `GET` | `/api/repositories/{id}/items/{itemId}/workspace` |
| Timeline | `GET` | `/api/repositories/{id}/items/{itemId}/timeline` |
| Comments | `GET` | `/api/repositories/{id}/items/{itemId}/comments` |
| File download / inline | `GET` | `/api/repositories/{id}/items/{itemId}/file` |

### 1.3 Create share

```http
POST /api/repositories/{repositoryId}/items/{itemId}/share
Authorization: Bearer <jwt>
X-Tenant-Id: <sharer-tenant-guid>
Content-Type: application/json
```

**Request body:**

```json
{
  "email": "recipient@example.com",
  "message": "Please review this invoice"
}
```

**Response `201`:**

```json
{
  "shareId": "guid",
  "shareToken": "abc123...",
  "sourceRepositoryId": "guid",
  "sourceItemId": "guid",
  "recipientEmail": "recipient@example.com",
  "expiresAtUtc": "2026-08-01T00:00:00Z",
  "shareUrl": "https://demoapp.ezofis.com/sign-in?shareToken=abc123...&email=recipient%40example.com"
}
```

Use `shareUrl` in emails (backend also sends email when mail settings are configured).

### 1.4 Shared with me (after login)

```http
GET /api/repositories/shared-with-me
Authorization: Bearer <jwt>
X-Tenant-Id: <recipient-tenant-guid>
```

**Response `200`:**

```json
[
  {
    "shareId": "guid",
    "shareToken": "abc123...",
    "sourceRepositoryId": "guid",
    "sourceItemId": "guid",
    "fileName": "Invoice-001.pdf",
    "sourceOrganizationName": "Acme Corp",
    "sharedAtUtc": "2026-07-01T10:00:00Z",
    "expiresAtUtc": "2026-07-31T10:00:00Z"
  }
]
```

Matched by **JWT email claim** = recipient email on the share.

### 1.5 Use existing functions — just add `shareToken`

**Important for frontend:** You do **not** need new API functions or a separate shared-file page. Reuse the **same functions** you already use for the normal file view (item, workspace, timeline, comments, file download). Only pass an **optional** `shareToken` when the user is viewing a shared file.

| Existing function (example) | Change needed |
|----------------------------|---------------|
| `getRepositoryItem(repoId, itemId)` | Add optional `shareToken` query param |
| `getItemWorkspace(repoId, itemId)` | Add optional `shareToken` query param |
| `getItemTimeline(repoId, itemId)` | Add optional `shareToken` query param |
| `getItemComments(repoId, itemId)` | Add optional `shareToken` query param |
| `getItemFile(repoId, itemId)` | Add optional `shareToken` query param |
| Normal tenant file view | Omit `shareToken` — works as today |

**Rule:** If `shareToken` is present → append to URL (or send header). If not present → call exactly as you do now.

#### Example — extend existing fetch (no new endpoint)

```javascript
// BEFORE (normal file view — unchanged)
function getItemFile(repositoryId, itemId) {
  return api.get(`/api/repositories/${repositoryId}/items/${itemId}/file`);
}

// AFTER (same function, optional shareToken)
function getItemFile(repositoryId, itemId, shareToken = null) {
  const params = shareToken ? `?sharedtoken=${encodeURIComponent(shareToken)}` : "";
  return api.get(`/api/repositories/${repositoryId}/items/${itemId}/file${params}`);
}
```

```javascript
// Same pattern for all existing item calls
function getItemWorkspace(repositoryId, itemId, shareToken = null) {
  const q = shareToken ? `?sharedtoken=${encodeURIComponent(shareToken)}` : "";
  return api.get(`/api/repositories/${repositoryId}/items/${itemId}/workspace${q}`);
}

function getItem(repositoryId, itemId, shareToken = null) {
  const q = shareToken ? `?sharedtoken=${encodeURIComponent(shareToken)}` : "";
  return api.get(`/api/repositories/${repositoryId}/items/${itemId}${q}`);
}
```

#### Where to get `shareToken` on the file view page

1. **From sign-in URL** (first time): `?shareToken=...` on `/sign-in` → keep in state/session after login.
2. **From shared-with-me list** (return visit): `GET /api/repositories/shared-with-me` → use `shareToken` + `sourceRepositoryId` + `sourceItemId` from each row.
3. **From create-share response** (sharer testing): `shareToken` in `POST .../share` response.

#### Open file view page (same route as normal)

```javascript
// Normal own-tenant file
navigate(`/repository/${repoId}/items/${itemId}`);

// Shared file — SAME page, pass token via query or app state
navigate(`/repository/${sourceRepositoryId}/items/${sourceItemId}?shareToken=${shareToken}`);
// On page load: read shareToken from URL/state and pass to ALL existing fetch functions
```

### 1.6 Open shared file (existing routes + token)

Pass the share token on **every** existing repository item call when viewing a shared file.

**Option A — query parameter (recommended):**

```
GET /api/repositories/{sourceRepositoryId}/items/{sourceItemId}?sharedtoken={shareToken}
GET /api/repositories/{sourceRepositoryId}/items/{sourceItemId}/workspace?sharedtoken={shareToken}
GET /api/repositories/{sourceRepositoryId}/items/{sourceItemId}/timeline?sharedtoken={shareToken}
GET /api/repositories/{sourceRepositoryId}/items/{sourceItemId}/comments?sharedtoken={shareToken}
GET /api/repositories/{sourceRepositoryId}/items/{sourceItemId}/file?sharedtoken={shareToken}&disposition=inline
```

**Option B — alternate query name:** `shareToken` (camelCase) also works.

**Option C — header (if you prefer not to change URL builders):**

```http
X-Share-Token: abc123...
```

**Required headers for shared file view:**

```http
Authorization: Bearer <recipient-jwt>
X-Tenant-Id: <recipient-tenant-guid>
```

Without `sharedtoken` / `shareToken` / `X-Share-Token`, the API looks up the repo in the **recipient's tenant only** → `Repository not found`.

### 1.7 Preview (before login)

```http
GET /api/repositories/share/{shareToken}/preview
```

No auth. Use on sign-in page to show file name and organization before login.

**Response `200`:**

```json
{
  "shareToken": "abc123...",
  "sourceRepositoryId": "guid",
  "sourceItemId": "guid",
  "fileName": "Invoice-001.pdf",
  "sourceOrganizationName": "Acme Corp",
  "recipientEmail": "recipient@example.com",
  "expiresAtUtc": "2026-07-31T10:00:00Z",
  "requiresLogin": true
}
```

### 1.8 Revoke share

```http
DELETE /api/repositories/share/{shareId}
Authorization: Bearer <jwt>
X-Tenant-Id: <sharer-tenant-guid>
```

**Response:** `204 No Content` on success.

### 1.9 Frontend implementation checklist

- [ ] **Reuse existing file view functions** — add optional `shareToken` param only; no duplicate APIs
- [ ] Sign-in page: read `shareToken` and `email` from URL (`?shareToken=...&email=...`)
- [ ] After login, if `shareToken` in URL → open **same** file view page; pass token to all existing fetch calls
- [ ] Add **Shared with me** section: `GET /api/repositories/shared-with-me`
- [ ] On file view: pass `sharedtoken` on item, workspace, timeline, comments, **file** fetches
- [ ] Do **not** allow edit/upload/comment POST when viewing via share token (API returns `403` for writes)
- [ ] Share button on file view: `POST .../share` with recipient email

### 1.10 Error cases

| Status | Meaning |
|--------|---------|
| `401` | Not logged in (share item routes) |
| `403` | Invalid/expired share, wrong recipient email, or write attempted on shared file |
| `404` | Share/repo/item not found |

---

## 2. Inbox / Sent fix (completed)

**No new API endpoints.** Behavior fix on existing mailbox routes.

### Problem (fixed)

When the **same user** was assigned a task, the item could appear in **both Inbox and Sent** at the same time.

### Fix (backend)

- **Sent list** excludes rows where the current user still has an **open inbox task** for that instance.
- **Mailbox sync** removes the sent row when an inbox row is inserted for the same assignee.

### APIs (unchanged — use as before)

| List | Method | Endpoint |
|------|--------|----------|
| Inbox | `GET` | `/api/workflows/inbox?workflowId={guid}&pageNumber=1&pageSize=20` |
| Sent | `GET` | `/api/workflows/sent?workflowId={guid}&pageNumber=1&pageSize=20` |
| Completed | `GET` | `/api/workflows/completed?workflowId={guid}` |
| Counts | `GET` | `/api/workflows/instance-count?workflowId={guid}` |
| Counts (all workflows) | `GET` | `/api/workflows/counts` |

### Frontend impact

- **No request/response shape changes.**
- After deploy, self-assigned items should appear in **Inbox only** (not duplicated in Sent).
- Refresh inbox/sent lists after approve/move-next to see updated placement.

---

## 3. Hangfire performance (backend)

**No frontend API changes.** This is a server configuration fix so the API stays responsive while background jobs run.

### What changed

| Setting | Before | After |
|---------|--------|-------|
| `Hangfire:ApiWorkerCount` | (default 10 via `WorkerCount`) | **5** (recommended) |
| Queue poll interval | ~few seconds | **15 seconds** |

Configured in `appsettings` / `appsettings.Production.json`:

```json
"Hangfire": {
  "RunServerInApi": true,
  "ApiWorkerCount": 5,
  "WorkerCount": 10
}
```

### Frontend impact

- API calls (inbox, repository, workflow) should feel faster under load.
- Background jobs (AP Agent, archive upload, master file import) still run; only parallel worker count in the API process is capped.
- Continue polling AP Agent job status as documented below; no change to poll interval required (still ~5 seconds).

---

## 4. AP Agent — multiple job status fetch

Poll **one or many** Hangfire job IDs in a single request (e.g. multiple invoices processing in parallel).

**Base path:** `/api/workflows`  
**Full doc:** see also `docs/AP_AGENT_STATUS_TRACKING.md`

### 4.1 Bulk status (query parameter)

```http
GET /api/workflows/ap-agent/jobs?jobIds=12345,67890,11111
Authorization: Bearer <jwt>
X-Tenant-Id: <tenant-guid>
```

### 4.2 Bulk status (path — comma-separated)

```http
GET /api/workflows/ap-agent/jobs/12345,67890,11111
```

### 4.3 Single job (unchanged)

```http
GET /api/workflows/ap-agent/jobs/{jobId}
```

### 4.4 Response (bulk)

```json
{
  "items": [
    {
      "jobId": "12345",
      "workflowId": "guid",
      "instanceId": "guid",
      "hangfireStatus": "Processing",
      "stage": "OCR_RUNNING",
      "message": "Running OCR on invoice PDF",
      "percent": 25,
      "formData": null,
      "errorMessage": null,
      "updatedAtUtc": "2026-07-02T10:00:00Z",
      "isTerminal": false
    }
  ],
  "notFoundJobIds": ["99999"]
}
```

### 4.5 Frontend polling pattern

```javascript
// After starting multiple AP Agent jobs, collect job IDs
const jobIds = ["12345", "67890", "11111"];

const interval = setInterval(async () => {
  const res = await fetch(
    `/api/workflows/ap-agent/jobs?jobIds=${jobIds.join(",")}`,
    { headers: { Authorization: `Bearer ${token}`, "X-Tenant-Id": tenantId } }
  );
  const data = await res.json();

  for (const item of data.items) {
    updateJobUI(item.jobId, item.stage, item.message, item.percent, item.isTerminal);
  }

  const allDone = data.items.length > 0 && data.items.every((j) => j.isTerminal);
  if (allDone) clearInterval(interval);
}, 5000);
```

Stop polling when every job has `isTerminal: true`.

---

## 5. Bulk move-next

Move **multiple workflow instances** to the next step in one API call. No `formData` — ticket move only.

```http
POST /api/workflows/instances/bulk-move-next
Authorization: Bearer <jwt>
X-Tenant-Id: <tenant-guid>
Content-Type: application/json
```

### 5.1 Request body

**Option A — comma-separated string:**

```json
{
  "instanceId": "guid1,guid2,guid3",
  "activityid": "ACTIVITY_BLOCK_ID_FROM_WORKFLOW",
  "review": "Approve",
  "comments": "Bulk approved",
  "activityUserId": "optional-user-guid"
}
```

**Option B — array:**

```json
{
  "instanceIds": ["guid1", "guid2", "guid3"],
  "activityid": "ACTIVITY_BLOCK_ID_FROM_WORKFLOW",
  "review": "Approve",
  "comments": "Bulk approved"
}
```

You can combine both `instanceId` and `instanceIds`; duplicates are removed.

| Field | Required | Notes |
|-------|----------|-------|
| `activityid` | Yes | Current activity/block id (JSON property name is lowercase `activityid`) |
| `instanceId` | One of | Comma-separated GUID string |
| `instanceIds` | One of | Array of GUIDs |
| `review` | No | e.g. `Approve`, `Reject` |
| `comments` | No | Comment text |
| `activityUserId` | No | Override acting user |

### 5.2 Response `200`

```json
{
  "total": 3,
  "succeeded": 2,
  "failed": 1,
  "results": [
    {
      "instanceId": "guid1",
      "success": true,
      "message": "Moved to next step.",
      "workflowCompleted": false,
      "error": null
    },
    {
      "instanceId": "guid2",
      "success": true,
      "message": "Workflow completed.",
      "workflowCompleted": true,
      "error": null
    },
    {
      "instanceId": "guid3",
      "success": false,
      "message": "Move failed.",
      "workflowCompleted": false,
      "error": "Instance not found or not assigned to user."
    }
  ]
}
```

### 5.3 Frontend usage

- Use for **multi-select approve** in inbox/grid views.
- Show per-instance success/failure from `results[]`.
- Refresh inbox/sent lists after bulk move (see [section 2](#2-inbox--sent-fix-completed)).
- For single instance with form data, continue using the existing move-next endpoint with `formData`.

### 5.4 Errors

| Status | Cause |
|--------|-------|
| `400` | Missing instance ids, invalid GUID, missing `activityid` |
| `401` | Not authenticated |

---

## Quick reference — all new/changed routes

| Feature | Method | Endpoint |
|---------|--------|----------|
| Create share | `POST` | `/api/repositories/{id}/items/{itemId}/share` |
| Shared with me | `GET` | `/api/repositories/shared-with-me` |
| Share preview | `GET` | `/api/repositories/share/{shareToken}/preview` |
| Revoke share | `DELETE` | `/api/repositories/share/{shareId}` |
| View shared item (use existing functions + token) | `GET` | `/api/repositories/{id}/items/{itemId}?sharedtoken=...` |
| View shared file blob (use existing `getItemFile` + token) | `GET` | `/api/repositories/{id}/items/{itemId}/file?sharedtoken=...` |
| AP Agent bulk status | `GET` | `/api/workflows/ap-agent/jobs?jobIds=id1,id2` |
| Bulk move-next | `POST` | `/api/workflows/instances/bulk-move-next` |
| Inbox | `GET` | `/api/workflows/inbox?workflowId=...` |
| Sent | `GET` | `/api/workflows/sent?workflowId=...` |

---

## Deployment note for frontend

Ensure the deployed API build includes PR #7 (`main` at merge `1217d38` or later). Catalog table `catalog.RepositoryItemShares` is created automatically on first share, or run `scripts/Create_RepositoryItemShares.sql`.

**Sign-in URL format for share emails:**

```
https://demoapp.ezofis.com/sign-in?shareToken={token}&email={recipientEmail}
```

Frontend should preserve `shareToken` through login and pass it to **existing** repository fetch functions on the file view page (item, workspace, timeline, comments, file).

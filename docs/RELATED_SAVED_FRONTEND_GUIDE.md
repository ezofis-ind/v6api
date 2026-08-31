# Related Saved Documents — Frontend Guide

**Audience:** Frontend  
**Last updated:** August 2026

Only the **saved** related-docs APIs. Auth: `Authorization: Bearer {jwt}`. Tenant comes from JWT / tenant context.

| Use case | Method | Endpoint |
|----------|--------|----------|
| Load saved related docs on item open | `GET` | `/api/repositories/{repositoryId}/items/{itemId}/related-saved` |
| Save / replace selection | `PUT` | `/api/repositories/{repositoryId}/items/{itemId}/related-saved` |
| Add extra link(s) (keep existing) | `POST` | `/api/repositories/{repositoryId}/items/{itemId}/related-saved` |
| Delete one link | `DELETE` | `/api/repositories/{repositoryId}/items/{itemId}/related-saved?relatedRepositoryId=&relatedItemId=` |
| Delete one link (by link id) | `DELETE` | `/api/repositories/{repositoryId}/items/{itemId}/related-saved/{linkId}` |

Path `{repositoryId}` + `{itemId}` = the **open** file.  
`PUT` **replaces** the full set. `POST` **appends** without clearing (e.g. 10 → 11). Empty `PUT items` clears all.  
`DELETE` soft-removes **one** link and returns the updated list.

---

## 1. GET — load saved related docs

```http
GET /api/repositories/{repositoryId}/items/{itemId}/related-saved
Authorization: Bearer {jwt}
```

Call this when opening an item. Opening any linked related file returns the **same family** (source + siblings), excluding the file currently open.

### Sample response

```json
{
  "sourceRepositoryId": "f1138fe5-ddfa-4daf-8562-11fa4e989f23",
  "sourceItemId": "f26ac1d1-8a13-4fe1-b353-b6b8d14f5e86",
  "matchField": null,
  "matchValue": null,
  "totalCount": 1,
  "data": [
    {
      "id": "link-guid",
      "relatedRepositoryId": "f1138fe5-ddfa-4daf-8562-11fa4e989f23",
      "relatedRepositoryName": "Accounts Payable",
      "relatedItemId": "2d692628-b881-45f5-9002-3f3ce158f4cd",
      "fileName": "INV-2026-3101_v8.pdf",
      "fileType": "application/pdf",
      "filePath": "monitor/f1138fe5-…/INV-2026-3101_v8.pdf",
      "fileSize": 204800,
      "documentType": "Invoice",
      "supplier": "ACME Corp",
      "poNumber": "PO-12345",
      "invoiceNumber": "INV-2026-3101",
      "matchScore": 93,
      "matchField": null,
      "matchValue": null,
      "createdAtUtc": "2026-08-11T10:00:00Z"
    }
  ]
}
```

### Response fields

| Field | Type | Meaning |
|-------|------|---------|
| `sourceRepositoryId` / `sourceItemId` | guid | Open file (path) |
| `matchField` / `matchValue` | string? | Set for single-field saves; `null` for overall |
| `totalCount` | int | Number of rows in `data` |
| `data[].id` | guid | Link row id — use for DELETE by linkId when it is a real saved link |
| `data[].relatedRepositoryId` | guid | Related file’s repo — use to open / download / **DELETE** |
| `data[].relatedItemId` | guid | Related file’s item id — use to open / download / **DELETE** |
| `data[].relatedRepositoryName` | string? | Repo name (saved snapshot preferred) |
| `data[].fileName` | string? | Snapshot file name |
| `data[].fileType` | string? | Snapshot type |
| `data[].filePath` | string? | Snapshot storage path (for Python / reopen) |
| `data[].fileSize` | int? | From live item when available |
| `data[].matchScore` | int? | Score at save time |
| `data[].documentType` / `supplier` / `poNumber` / `invoiceNumber` | string? | From live item when available |
| `data[].createdAtUtc` | datetime | When the link was saved |

### Open a related file from GET

Use **`relatedRepositoryId` + `relatedItemId`** (not `data[].id`):

```text
/repositories/{relatedRepositoryId}/items/{relatedItemId}
```

Preview / download:

```http
GET /api/repositories/{relatedRepositoryId}/items/{relatedItemId}/file?disposition=inline
```

---

## 2. PUT — save / replace selection

```http
PUT /api/repositories/{repositoryId}/items/{itemId}/related-saved
Authorization: Bearer {jwt}
Content-Type: application/json
```

Body `items[]` = each **related** file. Map search row `id` → body `itemId`.

### Request body

| Field | Type | Required | Notes |
|-------|------|----------|--------|
| `matchField` | string? | No | Set with `matchValue` for single-field match; omit/`null` for overall |
| `matchValue` | string? | No | Same as above |
| `items` | array | **Yes** | Replace set; `[]` clears all |
| `items[].repositoryId` | guid | **Yes** | Related file’s repo |
| `items[].itemId` | guid | **Yes** | Related file’s item id |
| `items[].matchScore` | int? | No | Optional; omit if not available |
| `items[].fileName` | string? | No | Optional snapshot for Python / reopen |
| `items[].fileType` | string? | No | Optional snapshot |
| `items[].filePath` | string? | No | Optional snapshot storage path |
| `items[].repositoryName` | string? | No | Optional snapshot repo display name |

Only `repositoryId` + `itemId` are required. If `fileName` / `fileType` / `filePath` / `repositoryName` are omitted, the API fills them from the live related item when possible. `matchScore` may be omitted (`null`).

### Overall match (recommended body)

```json
{
  "items": [
    {
      "repositoryId": "f1138fe5-ddfa-4daf-8562-11fa4e989f23",
      "itemId": "2d692628-b881-45f5-9002-3f3ce158f4cd",
      "matchScore": 93,
      "fileName": "INV-2026-3101_v8.pdf",
      "fileType": "application/pdf",
      "filePath": "monitor/…/INV-2026-3101_v8.pdf",
      "repositoryName": "Accounts Payable"
    }
  ]
}
```

### Minimal body (only required fields)

```json
{
  "items": [
    {
      "repositoryId": "f1138fe5-ddfa-4daf-8562-11fa4e989f23",
      "itemId": "2d692628-b881-45f5-9002-3f3ce158f4cd"
    }
  ]
}
```

`matchScore` is **not** mandatory.

### Particular field match

```json
{
  "matchField": "Supplier",
  "matchValue": "Nexus Industrial Solutions Ltd.",
  "items": [
    {
      "repositoryId": "11111111-2222-3333-4444-555555555555",
      "itemId": "bbbbbbbb-cccc-dddd-eeee-ffffffffffff",
      "matchScore": 100,
      "fileName": "PO-77291_v9.pdf",
      "fileType": "application/pdf",
      "filePath": "monitor/…/PO-77291_v9.pdf",
      "repositoryName": "Test Invoice 2026"
    }
  ]
}
```

### Clear all saved related docs

```json
{
  "items": []
}
```

### PUT response

Same shape as **GET** (latest saved set after replace).

---

## 3. POST — add extra link(s) without clearing

Use when the item already has saved related files and the user adds more (e.g. **10 → 11**).  
Existing links stay; only new pairs are inserted. Already-linked pairs are skipped.

```http
POST /api/repositories/{repositoryId}/items/{itemId}/related-saved
Authorization: Bearer {jwt}
Content-Type: application/json
```

### Body (same shape as PUT — send only the new file(s))

```json
{
  "items": [
    {
      "repositoryId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "itemId": "bbbbbbbb-cccc-dddd-eeee-ffffffffffff",
      "matchScore": 88,
      "fileName": "PO-60002.pdf",
      "fileType": "application/pdf",
      "filePath": "monitor/…/PO-60002.pdf",
      "repositoryName": "Accounts Payable"
    }
  ]
}
```

### POST response

Same shape as **GET** (full list including the new link(s)).

---

## 4. DELETE — remove one link

**Preferred** (works for every row in GET `data[]`):

```http
DELETE /api/repositories/{repositoryId}/items/{itemId}/related-saved?relatedRepositoryId={relatedRepositoryId}&relatedItemId={relatedItemId}
Authorization: Bearer {jwt}
```

Use values from the row the user removes:

- path = **open** file (`repositoryId` + `itemId`)
- query = row’s **`relatedRepositoryId`** + **`relatedItemId`**

Example:

```http
DELETE /api/repositories/f26a…/items/f26ac1d1-…/related-saved?relatedRepositoryId=f1138fe5-…&relatedItemId=2d692628-…
```

**Optional** — by saved link id:

```http
DELETE /api/repositories/{repositoryId}/items/{itemId}/related-saved/{linkId}
Authorization: Bearer {jwt}
```

Use `data[].id` only when it is a real DB link id. Prefer related pair for family / synthetic rows.

### DELETE response

Same shape as **GET** (remaining links after soft-delete).

---

## 5. FE wiring

```text
On open item:
  → GET …/related-saved
  → list data[] using relatedRepositoryId + relatedItemId

On first save / full replace:
  → PUT …/related-saved
  → body items: all selected related files

On add one more (keep existing 10, add 11th):
  → POST …/related-saved
  → body items: [ { only the new file } ]
  → replace UI list with POST response data

On remove one link (trash / unlink):
  → DELETE …/related-saved?relatedRepositoryId={row.relatedRepositoryId}&relatedItemId={row.relatedItemId}
  → replace UI list with DELETE response data
```

---

## 6. Errors

| Status | When |
|--------|------|
| `400` | DELETE without `linkId` and without `relatedRepositoryId` + `relatedItemId` |
| `401` | Missing / invalid token |
| `403` | No view access to source repo/item |
| `404` | Source item or repository not found |
| `200` + empty `data` | No saved related docs (or all deleted) |

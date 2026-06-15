# Workflow Requirements Specification

Requirements for workflow schema and behavior changes. Use this for design review and implementation planning.

---

## 1. Per-Workflow Instance Tables

**Requirement:** `WorkflowInstances` must be a separate table per workflow to avoid bulk rows in a single table per tenant.

**Current:** `workflow.WorkflowInstances` (shared – all instances of all workflows in one table)

**Target:** `workflow.WorkflowInstances_{suffix}` (per workflow, like `WorkflowComments_{suffix}`)

| Table | Current | Target |
|-------|---------|--------|
| WorkflowInstances | Shared | `WorkflowInstances_{suffix}` per workflow |
| WorkflowStepInstances | Shared | `WorkflowStepInstances_{suffix}` per workflow |

**Benefits:**
- Smaller tables per workflow
- Better query performance per workflow
- Easier archival/cleanup per workflow
- Aligns with existing per-workflow Comments, Attachments, etc.

**Implementation notes:**
- Create `WorkflowInstances_{suffix}` and `WorkflowStepInstances_{suffix}` in `WorkflowTableCreator` on publish
- Update `WorkflowRepository` to use dynamic table name based on `workflowId`
- EF Core may need raw SQL or a separate DbContext per workflow; alternatively use `IDynamicTableRepository` pattern for instances

---

## 2. Approve/Reject Branching (Different Next Stages)

**Requirement:** Approve and Reject must route to different next stages.

**Current:** Approve → next step; Reject → cancel workflow (or no alternate path)

**Target:**

```
                    ┌─────────────────┐
                    │ Finance Approval │
                    └────────┬────────┘
                             │
              ┌──────────────┼──────────────┐
              │ Approve      │              │ Reject
              ▼              │              ▼
     ┌─────────────────┐    │     ┌─────────────────┐
     │ Manager Approval│    │     │ Revision Request│
     │ (next stage)     │    │     │ (alternate stage)│
     └─────────────────┘    │     └─────────────────┘
                            │
                            │     Or: Reassign / Send back to Submit
```

**Schema changes:**
- `WorkflowStep`: add `ApprovedNextStepId`, `RejectedNextStepId` (nullable)
- Or: `WorkflowStepConfig` JSON with `{ "onApprove": "step-id", "onReject": "step-id" }`
- Support branching in `ApproveStepCommandHandler` and `RejectStepCommandHandler`

**Example config (JSON):**
```json
{
  "onApprove": { "nextStepId": "guid" },
  "onReject": { "nextStepId": "guid" }
}
```

---

## 3. Approval Types: All Must Approve vs Any One Approve

**Requirement:** Two approval modes:
- **All must approve:** 3 approvers → all 3 must approve to move to next stage
- **Any one approve:** 3 approvers → any one approval moves to next stage

**Current:** Single approver per step (AssignedToUserId or AssignedToRole)

**Target:** Support multiple approvers with approval policy.

| Policy | Description | Move condition |
|--------|-------------|----------------|
| AllMustApprove | All assigned approvers must approve | Count(Approved) = Count(Assigned) |
| AnyOneApprove | First approval moves forward | Count(Approved) >= 1 |

**Schema changes:**
- `WorkflowStep`: add `ApprovalPolicy` (enum: AllMustApprove, AnyOneApprove)
- `WorkflowStep`: support multiple assignees (new table `WorkflowStepAssignees` or JSON array in Config)
- `WorkflowApprovals`: already has per-approver rows; use to track who approved

**Example Config:**
```json
{
  "approvalPolicy": "AllMustApprove",
  "approvers": ["user-id-1", "user-id-2", "user-id-3"]
}
```

Or:
```json
{
  "approvalPolicy": "AnyOneApprove",
  "approvers": ["user-id-1", "user-id-2", "user-id-3"]
}
```

**Logic:**
- On Approve: check policy; if AllMustApprove, count approvals; if AnyOneApprove, move immediately
- On Reject: per requirement #2, route to reject branch

---

## 4. Read/Unread (Opened but Not Actioned)

**Requirement:** UI needs to show whether a ticket/instance has been opened but not actioned (like read/unread).

**Use case:** "This ticket is in my inbox but I haven't looked at it yet" vs "I've opened it but not approved/rejected"

**Schema options:**

| Option | Table | Columns | Description |
|--------|-------|---------|-------------|
| A | WorkflowInstances | `LastViewedAtUtc`, `LastViewedBy` | When anyone last viewed |
| B | WorkflowInstanceViews | `InstanceId`, `UserId`, `ViewedAtUtc`, `ActionedAtUtc` | Per-user view + action |
| C | WorkflowInstanceUserState | `InstanceId`, `UserId`, `IsRead`, `ReadAtUtc`, `IsActioned` | Explicit read/action flags |

**Recommended: Option C (or B extended)**

```
workflow.WorkflowInstanceUserState_{suffix}
├── Id
├── WorkflowInstanceId
├── UserId (assignee or relevant user)
├── IsRead (bool)
├── ReadAtUtc
├── IsActioned (bool)  -- approved, rejected, or completed action
├── ActionedAtUtc
└── CreatedAtUtc
```

**API:**
- `POST /instances/{id}/mark-read` – set IsRead = true, ReadAtUtc = now
- `GET /instances` or inbox – include `isRead`, `isActioned` in response
- UI: badge or style for unread / not actioned

---

## 5. Superuser vs Admin View

**Requirement:** Different views and permissions for Superuser vs Admin on workflows.

| Role | Capabilities |
|------|--------------|
| **Admin** | Manage workflows within own tenant; create/edit/publish workflows; view instances for own tenant |
| **Superuser** | Cross-tenant view; see all tenants’ workflows and instances; override/impersonate; system-level actions |

**Implementation approach:**

### 5.1 Role definitions
- `Admin` – tenant-scoped (existing)
- `Superuser` – catalog/global scope; stored in `catalog.UserTenants` or `catalog.Users` with `IsSuperuser`

### 5.2 API / query behavior
- **Admin:** All queries filtered by `TenantId` from `ITenantContext`
- **Superuser:** Can pass `?tenantId=xxx` to view another tenant; or `X-Tenant-Id` override; queries not restricted to single tenant when in superuser mode

### 5.3 Endpoints
| Endpoint | Admin | Superuser |
|----------|-------|-----------|
| GET /Workflows | Own tenant | All tenants (with tenant filter) |
| GET /Workflows/{id}/instances | Own tenant | Any tenant |
| POST /Workflows/setup-schema | Own tenant | Any tenant |
| GET /tenants (if exists) | No | Yes – list all tenants |

### 5.4 UI
- Admin: tenant selector fixed to own tenant
- Superuser: tenant selector to switch context; "View as tenant X"

### 5.5 Schema
- `catalog.Users` or `catalog.UserTenants`: add `IsSuperuser` (bit)
- Or: separate `catalog.Superusers` table with UserId

---

## Summary: Implementation Order

| # | Requirement | Effort | Dependencies |
|---|-------------|--------|--------------|
| 1 | Per-workflow WorkflowInstances table | High | WorkflowTableCreator, Repository refactor |
| 2 | Approve/Reject branching | Medium | WorkflowStep schema, handler changes |
| 3 | All/Any approval policy | Medium | WorkflowStep config, approval logic |
| 4 | Read/Unread (UserState) | Medium | New table, API, UI |
| 5 | Superuser vs Admin view | Medium | Auth, catalog schema, API filters |

---

## Related Docs

- [WORKFLOW_PER_WORKFLOW_TABLES_SAMPLE.md](WORKFLOW_PER_WORKFLOW_TABLES_SAMPLE.md) – Current per-workflow table samples
- [WORKFLOW_SCHEMA_DESIGN.md](WORKFLOW_SCHEMA_DESIGN.md) – Schema design options

# Workflow Schema Design – Current vs Expected

## Visual Overview

```
                    ┌──────────────────┐
                    │    Workflows     │  (shared)
                    └────────┬─────────┘
                             │
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
     ┌─────────────┐ ┌─────────────┐ ┌─────────────────────────────┐
     │WorkflowSteps│ │ WorkflowSlas│ │ WorkflowComments_{suffix}   │
     │  (shared)   │ │  (shared)   │ │ WorkflowAttachments_{suffix} │
     └──────┬──────┘ └─────────────┘ │ (per-workflow, on publish)  │
            │                         └─────────────────────────────┘
            │
            ▼
     ┌─────────────────────┐
     │ WorkflowInstances   │  (shared - ALL instances)
     └──────────┬──────────┘
                │
     ┌──────────┼──────────┐
     ▼          ▼          ▼
┌─────────────┐ ┌────────────────────┐ ┌──────────────────────┐
│WorkflowStep │ │WorkflowInstances   │ │ WorkflowInstanceSlas  │
│ Instances   │ │    History         │ │ (shared)              │
│ (shared)    │ │ (temporal/audit)   │ └──────────────────────┘
└─────────────┘ └────────────────────┘

❌ MISSING: WorkflowInstanceActions (who did what: Start, Approve, etc.)
```

---

## 1. Current Table Layout

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                         SHARED TABLES (all workflows in one table)                       │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│  workflow.Workflows              │ Workflow definitions (Draft/Active/Archived)          │
│  workflow.WorkflowSteps          │ Step definitions per workflow (Task, Approval, etc.)   │
│  workflow.WorkflowInstances      │ All instances of all workflows                        │
│  workflow.WorkflowInstancesHistory│ Temporal history of WorkflowInstances (SQL Server)    │
│  workflow.WorkflowStepInstances  │ Step execution per instance                          │
│  workflow.WorkflowApprovals      │ Approval requests (when Approval step used)            │
│  workflow.WorkflowSlas           │ SLA policy per workflow                              │
│  workflow.WorkflowInstanceSlas   │ SLA tracking per instance                             │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│              PER-WORKFLOW TABLES (created when workflow is PUBLISHED)                    │
│              Suffix = first 8 chars of workflow GUID (no hyphens)                         │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│  workflow.WorkflowComments_{suffix}     │ Comments for instances of THIS workflow only  │
│  workflow.WorkflowAttachments_{suffix}   │ Attachments for instances of THIS workflow    │
│  workflow.WorkflowForms_{suffix}        │ Form data per instance                         │
│  workflow.WorkflowTasks_{suffix}        │ Tasks per instance                             │
│  workflow.WorkflowSignatures_{suffix}   │ Signatures per instance                        │
│  workflow.WorkflowDocuments_{suffix}    │ Documents per workflow/instance                │
│  workflow.WorkflowEmails_{suffix}      │ Emails sent per instance                       │
│  workflow.WorkflowAiValidations_{suffix}│ AI validation results                           │
│  workflow.WorkflowPdfAnnotations_{suffix}│ PDF annotations                                │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. What You Expected vs Current

| Your expectation | Current design | Notes |
|------------------|---------------|-------|
| Each workflow has its own **instance** table | `WorkflowInstances` is shared | All instances in one table; `WorkflowId` column filters |
| Each workflow has its own **comments** table | `WorkflowComments_{suffix}` per workflow | Already per-workflow |
| Each workflow has its own **step instances** table | `WorkflowStepInstances` is shared | All step instances in one table |
| **Action/audit log** – who did what | Not implemented | No explicit action log table |

---

## 3. Design Options

### Option A: Keep current (shared instances + per-workflow content)

```
Workflows (shared) ──► WorkflowInstances (shared) ──► WorkflowStepInstances (shared)
       │                           │
       │                           └──► WorkflowComments_{wf} (per workflow)
       │                           └──► WorkflowAttachments_{wf} (per workflow)
       └──► WorkflowSteps (shared)
```

Pros: Simple queries, joins, reporting.  
Cons: Instances shared in one table.

---

### Option B: Per-workflow for everything (instances + steps)

```
Workflows (shared) ──► WorkflowInstances_{wf} (per workflow)
       │                           │
       │                           └──► WorkflowStepInstances_{wf} (per workflow)
       │                           └──► WorkflowComments_{wf} (per workflow)
       └──► WorkflowSteps (shared)
```

Pros: Full isolation per workflow.  
Cons: Per-workflow tables for instances/steps; more schema changes.

---

### Option C: Add action/audit log (recommended)

Add a new table to record **who did what**:

```
workflow.WorkflowInstanceActions
├── Id
├── TenantId
├── WorkflowInstanceId
├── WorkflowId
├── StepInstanceId (nullable)
├── ActionType (Start, MoveToNext, Approve, Reject, AddComment, Cancel, etc.)
├── PerformedBy (UserId)
├── PerformedAtUtc
├── Comments
├── Result
└── MetadataJson (optional extra data)
```

---

## 4. Sample Data Flow (E2E)

```
User action                    Table(s) affected
─────────────────────────────────────────────────────────────────────────
1. Create workflow             workflow.Workflows
2. Add steps                   workflow.WorkflowSteps
3. Publish                     workflow.Workflows (status), WorkflowComments_{suffix} (table created)
4. Start instance              workflow.WorkflowInstances, workflow.WorkflowStepInstances
5. Add comment                 workflow.WorkflowComments_{suffix}
6. Move to next step           workflow.WorkflowStepInstances (complete step 1, start step 2)
7. Approve step                workflow.WorkflowStepInstances (complete step 2)
8. Workflow completes          workflow.WorkflowInstances (status = Completed)
```

---

## 5. What’s Missing Today

| Gap | Description |
|-----|-------------|
| 1. Action log | No table that records who performed Start, MoveToNext, Approve, Reject, AddComment, etc. |
| 2. Per-workflow instances | No `WorkflowInstances_{suffix}` table; instances are shared. |
| 3. Per-workflow step instances | No `WorkflowStepInstances_{suffix}` table; step instances are shared. |
| 4. Dynamic table creation | `WorkflowTableCreator` may fail silently on publish; logs warning but does not fail. |

---

## 6. Recommended Next Steps

1. Confirm choice:
   - Option A: Keep current (shared instances + per-workflow content)
   - Option B: Move to per-workflow instances and step instances
   - Option C: Add `WorkflowInstanceActions` for audit (can be combined with A or B)

2. If Option C: add `WorkflowInstanceActions` table and log from each command handler.

3. If Option B: add `WorkflowInstances_{suffix}` and `WorkflowStepInstances_{suffix}` and adjust EF and repositories.

4. Verify dynamic tables: ensure `WorkflowTableCreator` runs successfully on publish.

---

## 7. Quick Reference – Tables Populated by E2E Test

| Table | Populated by |
|-------|--------------|
| workflow.Workflows | Create workflow |
| workflow.WorkflowSteps | Add steps |
| workflow.WorkflowInstances | Start instance |
| workflow.WorkflowInstancesHistory | SQL temporal (auto) |
| workflow.WorkflowStepInstances | Start instance |
| workflow.WorkflowComments_{suffix} | Add comment, MoveToNext, Approve |
| workflow.WorkflowInstanceActions | Not yet implemented |

---

## 8. Invoice Approval – Per-Workflow Sample (5 Steps)

**See:** [WORKFLOW_PER_WORKFLOW_TABLES_SAMPLE.md](WORKFLOW_PER_WORKFLOW_TABLES_SAMPLE.md) for full per-workflow table format and sample values.


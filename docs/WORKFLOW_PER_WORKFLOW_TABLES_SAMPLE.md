# Workflow – Per-Workflow Tables (Invoice Approval Sample)

**Format:** Each workflow has its own set of tables. Suffix = first 8 chars of WorkflowId (GUID without hyphens).

**Example:** WorkflowId `B2000000-0000-0000-0000-000000000002` → suffix `b2000000`

---

## Invoice Approval – 5 Steps

| Step | Type | Order | Description |
|------|------|-------|--------------|
| Submit Invoice | Manual (0) | 1 | Requester submits invoice |
| Department Review | Manual (0) | 2 | Dept head verifies |
| Finance Approval | Approval (1) | 3 | Finance approves/rejects |
| Manager Approval | Approval (1) | 4 | Manager approves/rejects |
| Final Approval | Approval (1) | 5 | Final sign-off |

---

## Per-Workflow Tables (suffix = `b2000000`)

### workflow.WorkflowInstances_b2000000

| Id | TenantId | WorkflowName | Status | CurrentStepInstanceId | StartedBy | ReferenceNumber | Priority | CreatedAtUtc | CompletedAtUtc |
|----|----------|--------------|--------|------------------------|-----------|------------------|----------|--------------|----------------|
| C3000000-0000-0000-0000-000000000003 | A1000000-0000-0000-0000-000000000001 | Invoice Approval | 3 (Completed) | E5000005-0000-0000-0000-000000000005 | U1000001-0000-0000-0000-000000000001 | INV-2025-001 | 2 (High) | 2025-02-26 10:00 | 2025-02-26 11:30 |

---

### workflow.WorkflowStepInstances_b2000000

| Id | WorkflowInstanceId | StepName | StepType | Order | Status | AssignedToUserId | StartedAtUtc | CompletedAtUtc | CompletedBy |
|----|--------------------|----------|----------|-------|--------|------------------|--------------|----------------|-------------|
| E5000001-0000-0000-0000-000000000001 | C3000000-0000-0000-0000-000000000003 | Submit Invoice | 0 (Manual) | 1 | 2 (Completed) | U1000001-0000-0000-0000-000000000001 | 2025-02-26 10:00 | 2025-02-26 10:15 | U1000001-0000-0000-0000-000000000001 |
| E5000002-0000-0000-0000-000000000002 | C3000000-0000-0000-0000-000000000003 | Department Review | 0 (Manual) | 2 | 2 (Completed) | U1000002-0000-0000-0000-000000000002 | 2025-02-26 10:15 | 2025-02-26 10:30 | U1000002-0000-0000-0000-000000000002 |
| E5000003-0000-0000-0000-000000000003 | C3000000-0000-0000-0000-000000000003 | Finance Approval | 1 (Approval) | 3 | 2 (Completed) | U1000003-0000-0000-0000-000000000003 | 2025-02-26 10:30 | 2025-02-26 11:00 | U1000003-0000-0000-0000-000000000003 |
| E5000004-0000-0000-0000-000000000004 | C3000000-0000-0000-0000-000000000003 | Manager Approval | 1 (Approval) | 4 | 2 (Completed) | U1000004-0000-0000-0000-000000000004 | 2025-02-26 11:00 | 2025-02-26 11:15 | U1000004-0000-0000-0000-000000000004 |
| E5000005-0000-0000-0000-000000000005 | C3000000-0000-0000-0000-000000000003 | Final Approval | 1 (Approval) | 5 | 2 (Completed) | U1000005-0000-0000-0000-000000000005 | 2025-02-26 11:30 | 2025-02-26 11:30 | U1000005-0000-0000-0000-000000000005 |

---

### workflow.WorkflowComments_b2000000

| Id | TenantId | WorkflowInstanceId | StepInstanceId | Comments | CreatedBy | CreatedAtUtc |
|----|----------|--------------------|----------------|----------|-----------|--------------|
| F6000001-0000-0000-0000-000000000001 | A1000000-... | C3000000-... | NULL | Invoice #INV-2025-001 submitted. Amount: $5,000 | U1000001-... | 2025-02-26 10:00 |
| F6000002-0000-0000-0000-000000000002 | A1000000-... | C3000000-... | E5000001-... | [STEP COMPLETED] Invoice details verified | U1000001-... | 2025-02-26 10:15 |
| F6000003-0000-0000-0000-000000000003 | A1000000-... | C3000000-... | E5000002-... | [STEP COMPLETED] Dept review done | U1000002-... | 2025-02-26 10:30 |
| F6000004-0000-0000-0000-000000000004 | A1000000-... | C3000000-... | E5000003-... | [APPROVED] Finance approved | U1000003-... | 2025-02-26 11:00 |
| F6000005-0000-0000-0000-000000000005 | A1000000-... | C3000000-... | E5000004-... | [APPROVED] Manager approved | U1000004-... | 2025-02-26 11:15 |
| F6000006-0000-0000-0000-000000000006 | A1000000-... | C3000000-... | E5000005-... | [APPROVED] Final approval – payment released | U1000005-... | 2025-02-26 11:30 |

---

### workflow.WorkflowApprovals_b2000000

| Id | TenantId | WorkflowInstanceId | StepInstanceId | RequestedBy | AssignedToUserId | Status | RespondedAtUtc | RespondedBy | Comments |
|----|----------|--------------------|----------------|-------------|-----------------|--------|----------------|-------------|----------|
| G7000001-0000-0000-0000-000000000001 | A1000000-... | C3000000-... | E5000003-... | U1000002-... | U1000003-... | 1 (Approved) | 2025-02-26 11:00 | U1000003-... | Finance approved |
| G7000002-0000-0000-0000-000000000002 | A1000000-... | C3000000-... | E5000004-... | U1000003-... | U1000004-... | 1 (Approved) | 2025-02-26 11:15 | U1000004-... | Manager approved |
| G7000003-0000-0000-0000-000000000003 | A1000000-... | C3000000-... | E5000005-... | U1000004-... | U1000005-... | 1 (Approved) | 2025-02-26 11:30 | U1000005-... | Final approval |

---

### workflow.WorkflowInstanceActions_b2000000 (audit – who did what)

| Id | WorkflowInstanceId | StepInstanceId | ActionType | PerformedBy | PerformedAtUtc | Comments |
|----|--------------------|----------------|------------|-------------|----------------|----------|
| H8000001-0000-0000-0000-000000000001 | C3000000-... | NULL | Start | U1000001-... | 2025-02-26 10:00 | Invoice submitted |
| H8000002-0000-0000-0000-000000000002 | C3000000-... | E5000001-... | AddComment | U1000001-... | 2025-02-26 10:00 | Invoice #INV-2025-001 submitted |
| H8000003-0000-0000-0000-000000000003 | C3000000-... | E5000001-... | MoveToNext | U1000001-... | 2025-02-26 10:15 | Invoice details verified |
| H8000004-0000-0000-0000-000000000004 | C3000000-... | E5000002-... | MoveToNext | U1000002-... | 2025-02-26 10:30 | Dept review done |
| H8000005-0000-0000-0000-000000000005 | C3000000-... | E5000003-... | Approve | U1000003-... | 2025-02-26 11:00 | Finance approved |
| H8000006-0000-0000-0000-000000000006 | C3000000-... | E5000004-... | Approve | U1000004-... | 2025-02-26 11:15 | Manager approved |
| H8000007-0000-0000-0000-000000000007 | C3000000-... | E5000005-... | Approve | U1000005-... | 2025-02-26 11:30 | Final approval – payment released |

---

### workflow.WorkflowAttachments_b2000000

| Id | TenantId | WorkflowInstanceId | StepInstanceId | FileName | FilePath | FileSize | CreatedBy | CreatedAtUtc |
|----|----------|--------------------|----------------|----------|----------|----------|-----------|--------------|
| I9000001-0000-0000-0000-000000000001 | A1000000-... | C3000000-... | E5000001-... | invoice_INV-2025-001.pdf | /uploads/inv/001.pdf | 245000 | U1000001-... | 2025-02-26 10:00 |

---

## REJECTED Flow (Reject at Step 3 – Finance Approval)

### workflow.WorkflowStepInstances_b2000000 (partial)

| Id | StepName | Order | Status | CompletedAtUtc | CompletedBy | ErrorMessage |
|----|----------|-------|--------|----------------|-------------|--------------|
| E5000001-... | Submit Invoice | 1 | 2 (Completed) | 2025-02-26 10:15 | U1000001-... | NULL |
| E5000002-... | Department Review | 2 | 2 (Completed) | 2025-02-26 10:30 | U1000002-... | NULL |
| E5000003-... | Finance Approval | 3 | 3 (Failed) | 2025-02-26 11:00 | U1000003-... | Cancelled/Rejected |

### workflow.WorkflowInstances_b2000000 (rejected)

| Id | Status | ErrorMessage |
|----|--------|--------------|
| C3000000-... | 5 (Cancelled) | Rejected at Finance Approval |

### workflow.WorkflowApprovals_b2000000 (rejected)

| Id | StepInstanceId | Status | RespondedBy | Comments |
|----|----------------|--------|-------------|----------|
| G7000001-... | E5000003-... | 2 (Rejected) | U1000003-... | Amount exceeds budget |

### workflow.WorkflowInstanceActions_b2000000 (rejected – last action)

| Id | StepInstanceId | ActionType | PerformedBy | PerformedAtUtc | Comments |
|----|----------------|------------|-------------|----------------|----------|
| H8000005-... | E5000003-... | Reject | U1000003-... | 2025-02-26 11:00 | Amount exceeds budget |

---

## Actions & API

| Action | API | Per-Workflow Table(s) Affected |
|--------|-----|-------------------------------|
| Start | POST /Workflows/{id}/start | WorkflowInstances_{suffix}, WorkflowStepInstances_{suffix}, WorkflowInstanceActions_{suffix} |
| Add comment | POST /instances/{id}/comments | WorkflowComments_{suffix}, WorkflowInstanceActions_{suffix} |
| Add attachment | POST /instances/{id}/attachments | WorkflowAttachments_{suffix} |
| MoveToNext | POST /instances/{id}/move-next | WorkflowStepInstances_{suffix}, WorkflowComments_{suffix}, WorkflowInstanceActions_{suffix} |
| Approve | POST /instances/{id}/steps/{stepId}/approve | WorkflowStepInstances_{suffix}, WorkflowApprovals_{suffix}, WorkflowComments_{suffix}, WorkflowInstanceActions_{suffix} |
| Reject | POST /instances/{id}/steps/{stepId}/reject | WorkflowStepInstances_{suffix}, WorkflowApprovals_{suffix}, WorkflowInstances_{suffix}, WorkflowInstanceActions_{suffix} |

---

## Per-Workflow Table List (created on Publish)

| Table | Purpose |
|-------|---------|
| workflow.WorkflowInstances_{suffix} | Instances of this workflow only |
| workflow.WorkflowStepInstances_{suffix} | Step execution per instance |
| workflow.WorkflowComments_{suffix} | Comments per instance |
| workflow.WorkflowAttachments_{suffix} | Attachments per instance |
| workflow.WorkflowApprovals_{suffix} | Approval requests/responses |
| workflow.WorkflowInstanceActions_{suffix} | Audit log – who did what |
| workflow.WorkflowForms_{suffix} | Form data |
| workflow.WorkflowTasks_{suffix} | Tasks |
| workflow.WorkflowSignatures_{suffix} | Signatures |
| workflow.WorkflowDocuments_{suffix} | Documents |
| workflow.WorkflowEmails_{suffix} | Emails sent |
| workflow.WorkflowAiValidations_{suffix} | AI validation results |
| workflow.WorkflowPdfAnnotations_{suffix} | PDF annotations |

---

## Enums

| Enum | Values |
|------|--------|
| StepType | 0=Manual, 1=Approval, 2=Automated, 3=Condition, 4=WaitForEvent, 5=Notification |
| WorkflowInstanceStatus | 0=Pending, 1=Running, 2=Paused, 3=Completed, 4=Failed, 5=Cancelled |
| StepInstanceStatus | 0=Pending, 1=InProgress, 2=Completed, 3=Failed, 4=Skipped, 5=WaitingForApproval |
| ApprovalStatus | 0=Pending, 1=Approved, 2=Rejected |
| ActionType | Start, AddComment, MoveToNext, Approve, Reject, AddAttachment, Cancel |

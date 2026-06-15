using MediatR;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Application.Workflows.Queries.GetLegacyMailboxInstanceCount;

/// <summary>Inbox / Sent / Completed counts for a workflow and the current user (legacy mailbox tables).</summary>
public sealed record GetLegacyMailboxInstanceCountQuery(Guid WorkflowId) : IRequest<LegacyMailboxInstanceCountResult>;

using MediatR;

namespace SaaSApp.Workflow.Application.Workflows.Commands.MoveToNextStep;

public sealed class BulkMoveToNextStepCommandHandler
    : IRequestHandler<BulkMoveToNextStepCommand, BulkMoveToNextStepCommandResult>
{
    private readonly IMediator _mediator;

    public BulkMoveToNextStepCommandHandler(IMediator mediator) => _mediator = mediator;

    public async Task<BulkMoveToNextStepCommandResult> Handle(
        BulkMoveToNextStepCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ActivityId))
            throw new ArgumentException("activityId is required.");

        if (request.InstanceIds.Count == 0)
            throw new ArgumentException("At least one instance id is required.");

        var results = new List<BulkMoveToNextStepItemResult>(request.InstanceIds.Count);

        foreach (var instanceId in request.InstanceIds)
        {
            try
            {
                var moveResult = await _mediator.Send(
                    new MoveToNextStepCommand(
                        instanceId,
                        request.ActivityId,
                        request.Review,
                        request.Comments,
                        request.ActivityUserId),
                    cancellationToken);

                results.Add(new BulkMoveToNextStepItemResult(
                    instanceId,
                    moveResult.Success,
                    moveResult.Message,
                    moveResult.WorkflowCompleted));
            }
            catch (Exception ex)
            {
                results.Add(new BulkMoveToNextStepItemResult(
                    instanceId,
                    false,
                    "Move failed.",
                    WorkflowCompleted: false,
                    Error: ex.Message));
            }
        }

        var succeeded = results.Count(r => r.Success);
        return new BulkMoveToNextStepCommandResult(
            results.Count,
            succeeded,
            results.Count - succeeded,
            results);
    }
}

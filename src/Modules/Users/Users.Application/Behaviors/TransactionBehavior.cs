using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior: persists changes and dispatches domain events after the handler runs.
/// Wraps the handler in a logical transaction (SaveChanges + event dispatch).
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(IServiceProvider serviceProvider, ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // This behavior is registered in DI globally; only apply unit-of-work for Users module requests.
        var requestNamespace = request.GetType().Namespace ?? string.Empty;
        if (!requestNamespace.StartsWith("SaaSApp.Users.Application", StringComparison.Ordinal))
        {
            return await next();
        }

        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        _logger.LogDebug("Transaction started for {RequestName}", typeof(TRequest).Name);
        var response = await next();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Transaction committed for {RequestName}", typeof(TRequest).Name);
        return response;
    }
}

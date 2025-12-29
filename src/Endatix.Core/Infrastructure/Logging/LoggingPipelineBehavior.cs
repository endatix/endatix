using System.Diagnostics;
using System.Reflection;
using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Attributes;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Infrastructure.Logging;

/// <summary>
/// Adds logging for all requests in MediatR pipeline.
/// Configure by adding the service with a scoped lifetime
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class LoggingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
  where TRequest : IRequest<TResponse>
{
    private readonly ILogger<Mediator> _logger;

    public LoggingPipelineBehavior(ILogger<Mediator> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);

            // Reflection! Could be a performance concern
            var myType = request.GetType();
            IList<PropertyInfo> props = [.. myType.GetProperties()];
            foreach (var prop in props)
            {
                var sensitiveAttribute = prop.GetCustomAttribute<SensitiveAttribute>();
                var value = prop.GetValue(request, null);

                var redactedValue = sensitiveAttribute is not null
                ? PiiRedactor.Redact(value, sensitiveAttribute.Type)
                : value;

                _logger.LogInformation("Property {Property} : {@Value}", prop?.Name, redactedValue);
            }
        }

        var sw = Stopwatch.StartNew();

        var response = await next();

        _logger.LogInformation("Handled {RequestName} with {Response} in {ms} ms", typeof(TRequest).Name, response, sw.ElapsedMilliseconds);
        sw.Stop();
        return response;
    }
}

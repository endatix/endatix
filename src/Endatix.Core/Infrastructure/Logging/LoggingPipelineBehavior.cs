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
    /// <summary>
    /// Shared category for every closed generic, so log filters target one name.
    /// </summary>
    internal const string LogCategory = "Endatix.Core.Infrastructure.Logging.LoggingPipelineBehavior";

    private readonly ILogger _logger;

    /// <summary>
    /// Creates the pipeline behavior with the shared <see cref="LogCategory"/> logger category.
    /// </summary>
    public LoggingPipelineBehavior(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(LogCategory);
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogRequestHandling(typeof(TRequest).Name);

            // Reflection! Could be a performance concern
            var myType = request.GetType();
            IList<PropertyInfo> props = [.. myType.GetProperties()];
            foreach (var prop in props)
            {
                var sensitiveAttribute = prop.GetCustomAttribute<SensitiveAttribute>();
                var value = prop.GetValue(request, null);

                var logValue = sensitiveAttribute is not null
                    ? PiiRedactor.Redact(value, sensitiveAttribute.Type)
                    : LogPropertyFormatter.FormatForLog(value);

                _logger.LogRequestProperty(prop.Name, logValue);
            }
        }

        var sw = Stopwatch.StartNew();

        var response = await next();

        _logger.LogRequestHandled(typeof(TRequest).Name, response, sw.ElapsedMilliseconds);
        return response;
    }
}

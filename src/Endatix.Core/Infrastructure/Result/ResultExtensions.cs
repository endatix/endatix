namespace Endatix.Core.Infrastructure.Result;

public static class ResultExtensions
{
    /// <summary>
    /// Transforms a Result's type from a source type to a destination type. If the Result is successful, the func parameter is invoked on the Result's source value to map it to a destination type.
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TDestination"></typeparam>
    /// <param name="result"></param>
    /// <param name="func"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static Result<TDestination> Map<TSource, TDestination>(this Result<TSource> result, Func<TSource, TDestination> func)
    {
        if (result.IsSuccess)
        {
            switch (result.Status)
            {
                case ResultStatus.Ok:
                    return func(result.Value);
                case ResultStatus.Created:
                    return string.IsNullOrEmpty(result.Location)
                        ? Result<TDestination>.Created(func(result.Value))
                        : Result<TDestination>.Created(func(result.Value), result.Location);
                case ResultStatus.NoContent:
                    return Result<TDestination>.NoContent();
                default:
                    throw new NotSupportedException($"Result {result.Status} conversion is not supported.");
            }
        }

        return result.ToErrorResult<TDestination>();
    }

    /// <summary>
    /// Converts an error result to an error result with a different payload type.
    /// </summary>
    /// <typeparam name="TDestination">The destination value type.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <returns>An error result preserving status and error payload.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when result is successful.</exception>
    /// <exception cref="NotSupportedException">Thrown if the result status is not supported.</exception>
    public static Result<TDestination> ToErrorResult<TDestination>(this IResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Status is ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent)
        {
            throw new InvalidOperationException("Result is successful. Cannot convert to error result.");
        }

        var correlationId = TryGetCorrelationId(result);

        return ConvertError<TDestination>(result.Status, result.Errors, result.ValidationErrors, correlationId);
    }

    private static Result<TDestination> ConvertError<TDestination>(
        ResultStatus status,
        IEnumerable<string> errors,
        IEnumerable<ValidationError> validationErrors,
        string correlationId)
    {
        switch (status)
        {
            case ResultStatus.NotFound:
                return errors.Any()
                    ? Result<TDestination>.NotFound(errors.ToArray())
                    : Result<TDestination>.NotFound();
            case ResultStatus.Unauthorized:
                return errors.Any()
                                    ? Result<TDestination>.Unauthorized(errors.ToArray())
                                    : Result<TDestination>.Unauthorized();
            case ResultStatus.Forbidden:
                return errors.Any()
                                    ? Result<TDestination>.Forbidden(errors.ToArray())
                                    : Result<TDestination>.Forbidden();
            case ResultStatus.Invalid:
                return Result<TDestination>.Invalid(validationErrors);
            case ResultStatus.Error:
                return Result<TDestination>.Error(new ErrorList(errors.ToArray(), correlationId));
            case ResultStatus.Conflict:
                return errors.Any()
                                    ? Result<TDestination>.Conflict(errors.ToArray())
                                    : Result<TDestination>.Conflict();
            case ResultStatus.CriticalError:
                return Result<TDestination>.CriticalError(errors.ToArray());
            case ResultStatus.Unavailable:
                return Result<TDestination>.Unavailable(errors.ToArray());
            default:
                throw new NotSupportedException($"Result {status} conversion is not supported.");
        }
    }

    private static string TryGetCorrelationId(IResult result)
    {
        var correlationIdProperty = result.GetType().GetProperty("CorrelationId");
        if (correlationIdProperty?.PropertyType != typeof(string))
        {
            return string.Empty;
        }

        return correlationIdProperty.GetValue(result) as string ?? string.Empty;
    }
}

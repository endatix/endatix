namespace Endatix.Core.Infrastructure.Result;

/// <summary>
/// Extension methods for the Result class.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Transforms a Result's type from a source type to a destination type. If the Result is successful, the func parameter is invoked on the Result's source value to map it to a destination type.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="func">The function to map the result.</param>
    /// <returns>The mapped result.</returns>
    public static Result<TDestination> Map<TSource, TDestination>(this Result<TSource> result, Func<TSource, TDestination> func)
    {
        if (result.IsSuccess)
        {
            return result.Status switch
            {
                ResultStatus.Ok => (Result<TDestination>)func(result.Value),
                ResultStatus.Created => string.IsNullOrEmpty(result.Location)
                                        ? Result<TDestination>.Created(func(result.Value))
                                        : Result<TDestination>.Created(func(result.Value), result.Location),
                ResultStatus.NoContent => Result<TDestination>.NoContent(),
                _ => throw new NotSupportedException($"Result {result.Status} conversion is not supported."),
            };
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
        string correlationId) => status switch
        {
            ResultStatus.NotFound => errors.Any()
                                ? Result<TDestination>.NotFound([.. errors])
                                : Result<TDestination>.NotFound(),
            ResultStatus.Unauthorized => errors.Any()
                                ? Result<TDestination>.Unauthorized([.. errors])
                                : Result<TDestination>.Unauthorized(),
            ResultStatus.Forbidden => errors.Any()
                                ? Result<TDestination>.Forbidden([.. errors])
                                : Result<TDestination>.Forbidden(),
            ResultStatus.Invalid => Result<TDestination>.Invalid([.. validationErrors]),
            ResultStatus.Error => errors.Any()
                                ? Result<TDestination>.Error(new ErrorList([.. errors], correlationId))
                                : Result<TDestination>.Error(new ErrorList([], correlationId)),
            ResultStatus.Conflict => errors.Any()
                                ? Result<TDestination>.Conflict([.. errors])
                                : Result<TDestination>.Conflict(),
            ResultStatus.CriticalError => errors.Any()
                                ? Result<TDestination>.CriticalError([.. errors])
                                : Result<TDestination>.CriticalError(),
            ResultStatus.Unavailable => errors.Any()
                                ? Result<TDestination>.Unavailable([.. errors])
                                : Result<TDestination>.Unavailable(),
            _ => throw new NotSupportedException($"Result {status} conversion is not supported."),
        };

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

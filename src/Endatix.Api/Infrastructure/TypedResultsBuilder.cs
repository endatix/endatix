using Microsoft.AspNetCore.Http.HttpResults;
using HttpResults = Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http;
using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Base class for mapping results to HTTP typed results.
/// </summary>
public abstract class TypedResultsBuilder
{
    /// <summary>
    /// Initializes a new instance of the TypedResultsBuilder with the specified result.
    /// </summary>
    /// <typeparam name="TData">The type of the data in the result.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <returns>A new instance of TypedResultsBuilder.</returns>
    public static TypedResultsBuilder<TData> FromResult<TData>(Result<TData> result) where TData : class
    {
        Guard.Against.Null(result);

        return new TypedResultsBuilder<TData>(result);
    }

    /// <summary>
    /// Initializes a new instance of the TypedResultsBuilder with the specified result and a mapper function.
    /// </summary>
    /// <typeparam name="TSource">The type of the data in the source result.</typeparam>
    /// <typeparam name="TDestination">The type of the data in the destination result.</typeparam>
    /// <param name="result">The source result to map.</param>
    /// <param name="func">The mapper function to transform the source result to the destination result.</param>
    /// <returns>A new instance of TypedResultsBuilder with the mapped result.</returns>
    public static TypedResultsBuilder<TDestination> MapResult<TSource, TDestination>(Result<TSource> result, Func<TSource, TDestination> func)
    where TSource : class
    where TDestination : class
    {
        Guard.Against.Null(result);
        Guard.Against.Null(func);

        var mappedResult = result.Map(func);
        return FromResult(mappedResult);
    }
}

/// <summary>
/// Maps results to HTTP typed results for a specific data type.
/// </summary>
/// <typeparam name="TData">The type of the data in the result.</typeparam>
public class TypedResultsBuilder<TData> : TypedResultsBuilder
    where TData : class
{
    /// <summary>
    /// The source result being mapped.
    /// </summary>
    public Result<TData> SourceResult { get; init; }

    /// <summary>
    /// The generic error message to be returned in case the source result is invalid.
    /// </summary>
    public string? ErrorMessage { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the TypedResultsBuilder with the specified source result.
    /// </summary>
    /// <param name="sourceResult">The source result to map.</param>
    protected internal TypedResultsBuilder(Result<TData> sourceResult)
    {
        Guard.Against.Null(sourceResult);

        SourceResult = sourceResult;
    }

    /// <summary>
    /// Sets the error message for the TypedResultsBuilder instance.
    /// </summary>
    /// <param name="errorMessage">The error message to set.</param>
    /// <returns>This instance of TypedResultsBuilder.</returns>
    public TypedResultsBuilder<TData> SetErrorMessage(string errorMessage)
    {
        Guard.Against.NullOrWhiteSpace(errorMessage);
        ErrorMessage = errorMessage;
        return this;
    }

    /// <summary>
    /// Produces a new TypedResultsBuilder instance that can map to additional result types.
    /// </summary>
    /// <typeparam name="TResult1">The first result type to map to.</typeparam>
    /// <typeparam name="TResult2">The second result type to map to.</typeparam>
    /// <returns>A new instance of TypedResultsBuilder that can map to TResult1 and TResult2.</returns>
    public TypedResultsBuilder<TData, TResult1, TResult2> SetTypedResults<TResult1, TResult2>()
          where TResult1 : HttpResults.IResult
          where TResult2 : HttpResults.IResult
    {
        return new TypedResultsBuilder<TData, TResult1, TResult2>(SourceResult, ErrorMessage);
    }
}

/// <summary>
/// Maps results to HTTP typed results for a specific data type and multiple result types.
/// </summary>
/// <typeparam name="TData">The type of the data in the result.</typeparam>
/// <typeparam name="TResult1">The first result type to map to.</typeparam>
/// <typeparam name="TResult2">The second result type to map to.</typeparam>
public class TypedResultsBuilder<TData, TResult1, TResult2> : TypedResultsBuilder<TData>
where TData : class
where TResult1 : HttpResults.IResult
where TResult2 : HttpResults.IResult
{
    /// <summary>
    /// Initializes a new instance of the TypedResultsBuilder with the specified source result and optional error message.
    /// </summary>
    /// <param name="sourceResult">The source result to map.</param>
    /// <param name="errorMessage">Optional error message to set.</param>
    protected internal TypedResultsBuilder(Result<TData> sourceResult, string? errorMessage = null) : base(sourceResult)
    {
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Implicitly converts the TypedResultsBuilder to a Results instance for Ok and ProblemDetails.
    /// </summary>
    /// <param name="resultMapper">The TypedResultsBuilder instance to convert.</param>
    /// <returns>A Results instance for Ok and ProblemDetails.</returns>
    public static implicit operator Results<Ok<TData>, ProblemHttpResult>(TypedResultsBuilder<TData, TResult1, TResult2> resultMapper)
    {
        return resultMapper.SourceResult.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(resultMapper.SourceResult.Value),
            // Success statuses this union cannot express mean the endpoint declared the wrong
            // Results<> signature - fail loudly rather than answer with a misleading status.
            ResultStatus.Created or ResultStatus.NoContent => throw new InvalidCastException(BuildCastErrorMessage(resultMapper)),
            // Every remaining status is a failure: RFC7807 problem+json, status picked by ToProblem.
            _ => resultMapper.SourceResult.ToProblem(resultMapper.ErrorMessage)
        };
    }

    /// <summary>
    /// Implicitly converts the TypedResultsBuilder to a Results instance for Created and ProblemHttpResult.
    /// </summary>
    /// <param name="resultMapper">The TypedResultsBuilder instance to convert.</param>
    /// <returns>A Results instance for Created and ProblemHttpResult.</returns>
    public static implicit operator Results<Created<TData>, ProblemHttpResult>(TypedResultsBuilder<TData, TResult1, TResult2> resultMapper)
    {
        return resultMapper.SourceResult.Status switch
        {
            ResultStatus.Created => TypedResults.Created(default(string), resultMapper.SourceResult.Value),
            // Success statuses this union cannot express mean the endpoint declared the wrong
            // Results<> signature - fail loudly rather than answer with a misleading status.
            ResultStatus.Ok or ResultStatus.NoContent => throw new InvalidCastException(BuildCastErrorMessage(resultMapper)),
            // Every remaining status is a failure: RFC7807 problem+json, status picked by ToProblem.
            _ => resultMapper.SourceResult.ToProblem(resultMapper.ErrorMessage)
        };
    }

    private static string BuildCastErrorMessage(TypedResultsBuilder<TData, TResult1, TResult2> resultMapper) =>
        $"Cannot cast the {resultMapper.SourceResult.ValueType} with status {resultMapper.SourceResult.Status}";
}

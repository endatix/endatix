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
    /// Initializes a new instance of the TypedResultsBuilder with the specified source result.
    /// </summary>
    /// <param name="sourceResult">The source result to map.</param>
    protected internal TypedResultsBuilder(Result<TData> sourceResult)
    {
        Guard.Against.Null(sourceResult);

        SourceResult = sourceResult;
    }

    /// <summary>
    /// Produces a new TypedResultsBuilder instance that can map to additional result types.
    /// </summary>
    /// <typeparam name="TResult1">The first result type to map to.</typeparam>
    /// <typeparam name="TResult2">The second result type to map to.</typeparam>
    /// <returns>A new instance of TypedResultsBuilder that can map to TResult1 and TResult2.</returns>
    public TypedResultsBuilder<TData, TResult1, TResult2> ConfigureResults<TResult1, TResult2>()
          where TResult1 : HttpResults.IResult
          where TResult2 : HttpResults.IResult
    {
        return new TypedResultsBuilder<TData, TResult1, TResult2>(SourceResult);
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
    /// Initializes a new instance of the TypedResultsBuilder with the specified source result.
    /// </summary>
    /// <param name="sourceResult">The source result to map.</param>
    protected internal TypedResultsBuilder(Result<TData> sourceResult) : base(sourceResult)
    {
    }

    /// <summary>
    /// Implicitly converts the TypedResultsBuilder to a Results instance for Ok and BadRequest.
    /// </summary>
    /// <param name="resultMapper">The TypedResultsBuilder instance to convert.</param>
    /// <returns>A Results instance for Ok and BadRequest.</returns>
    public static implicit operator Results<Ok<TData>, BadRequest>(TypedResultsBuilder<TData, TResult1, TResult2> resultMapper)
    {
        return resultMapper.SourceResult.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(resultMapper.SourceResult.Value),
            ResultStatus.Invalid => TypedResults.BadRequest(),
            _ => throw new InvalidCastException($"Cannot cast the {resultMapper.SourceResult.ValueType} with status {resultMapper.SourceResult.Status}")
        };
    }

    /// <summary>
    /// Implicitly converts the TypedResultsBuilder to a Results instance for Ok and NotFound.
    /// </summary>
    /// <param name="resultMapper">The TypedResultsBuilder instance to convert.</param>
    /// <returns>A Results instance for Ok and NotFound.</returns>
    public static implicit operator Results<Ok<TData>, NotFound>(TypedResultsBuilder<TData, TResult1, TResult2> resultMapper)
    {
        return resultMapper.SourceResult.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(resultMapper.SourceResult.Value),
            ResultStatus.NotFound => TypedResults.NotFound(),
            _ => throw new InvalidCastException($"Cannot cast the {resultMapper.SourceResult.ValueType} with status {resultMapper.SourceResult.Status}")
        };
    }
}
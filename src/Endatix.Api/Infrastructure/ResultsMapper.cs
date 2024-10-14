using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using NetCore = Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http;
using Ardalis.GuardClauses;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Base class for mapping results to HTTP typed results.
/// </summary>
public abstract class ResultMapper
{
    /// <summary>
    /// Initializes a new instance of the ResultsMapper with the specified result.
    /// </summary>
    /// <typeparam name="TData">The type of the data in the result.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <returns>A new instance of ResultsMapper.</returns>
    public static ResultsMapper<TData> WithData<TData>(Result<TData> result) where TData : class => new ResultsMapper<TData>(result);
}

/// <summary>
/// Maps results to HTTP typed results for a specific data type.
/// </summary>
/// <typeparam name="TData">The type of the data in the result.</typeparam>
public class ResultsMapper<TData> : ResultMapper
    where TData : class
{
    /// <summary>
    /// The source result being mapped.
    /// </summary>
    protected Result<TData> SourceResult { get; init; }

    /// <summary>
    /// Initializes a new instance of the ResultsMapper with the specified source result.
    /// </summary>
    /// <param name="sourceResult">The source result to map.</param>
    protected internal ResultsMapper(Result<TData> sourceResult)
    {
        Guard.Against.Null(sourceResult);

        SourceResult = sourceResult;
    }

    /// <summary>
    /// Produces a new ResultsMapper instance that can map to additional result types.
    /// </summary>
    /// <typeparam name="TResult1">The first result type to map to.</typeparam>
    /// <typeparam name="TResult2">The second result type to map to.</typeparam>
    /// <returns>A new instance of ResultsMapper that can map to TResult1 and TResult2.</returns>
    public ResultsMapper<TData, TResult1, TResult2> ProducesResults<TResult1, TResult2>()
          where TResult1 : NetCore.IResult
          where TResult2 : NetCore.IResult
    {
        return new ResultsMapper<TData, TResult1, TResult2>(SourceResult);
    }
}

/// <summary>
/// Maps results to HTTP typed results for a specific data type and multiple result types.
/// </summary>
/// <typeparam name="TData">The type of the data in the result.</typeparam>
/// <typeparam name="TResult1">The first result type to map to.</typeparam>
/// <typeparam name="TResult2">The second result type to map to.</typeparam>
public class ResultsMapper<TData, TResult1, TResult2> : ResultsMapper<TData>
where TData : class
where TResult1 : NetCore.IResult
where TResult2 : NetCore.IResult
{
    /// <summary>
    /// Initializes a new instance of the ResultsMapper with the specified source result.
    /// </summary>
    /// <param name="sourceResult">The source result to map.</param>
    protected internal ResultsMapper(Result<TData> sourceResult) : base(sourceResult)
    {
    }

    /// <summary>
    /// Implicitly converts the ResultsMapper to a Results instance for Ok and BadRequest.
    /// </summary>
    /// <param name="resultMapper">The ResultsMapper instance to convert.</param>
    /// <returns>A Results instance for Ok and BadRequest.</returns>
    public static implicit operator Results<Ok<TData>, BadRequest>(ResultsMapper<TData, TResult1, TResult2> resultMapper)
    {
        return resultMapper.SourceResult.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(resultMapper.SourceResult.Value),
            ResultStatus.Invalid => TypedResults.BadRequest(),
            _ => throw new InvalidCastException($"Cannot cast the {resultMapper.SourceResult.ValueType} with status {resultMapper.SourceResult.Status}")
        };
    }

    /// <summary>
    /// Implicitly converts the ResultsMapper to a Results instance for Ok and NotFound.
    /// </summary>
    /// <param name="resultMapper">The ResultsMapper instance to convert.</param>
    /// <returns>A Results instance for Ok and NotFound.</returns>
    public static implicit operator Results<Ok<TData>, NotFound>(ResultsMapper<TData, TResult1, TResult2> resultMapper)
    {
        return resultMapper.SourceResult.Status switch
        {
            ResultStatus.Ok => TypedResults.Ok(resultMapper.SourceResult.Value),
            ResultStatus.NotFound => TypedResults.NotFound(),
            _ => throw new InvalidCastException($"Cannot cast the {resultMapper.SourceResult.ValueType} with status {resultMapper.SourceResult.Status}")
        };
    }
}
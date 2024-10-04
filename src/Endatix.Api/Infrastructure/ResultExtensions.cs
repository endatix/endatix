using System.Text;
using Microsoft.AspNetCore.Mvc;
using Endatix.Core.Infrastructure.Result;
using AppDomain = Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Infrastructure;

#if NET7_0_OR_GREATER
public static partial class ResultExtensions
{
    /// <summary>
    /// Convert a <see cref="Result{TEntity}"/> to an instance of <c>Microsoft.AspNetCore.Http.HttpResults.Results&lt;,...,&gt;</c>
    /// </summary>
    /// <typeparam name="TResults">The Results object listing all the possible status endpoint responses</typeparam>
    /// <typeparam name="TEntity">The result entity type being received</typeparam>
    /// <typeparam name="TResponseModel">The HTTP endpoint response model value type being returned</typeparam>
    /// <param name="result">The command/query result to convert to an <c>Microsoft.AspNetCore.Http.HttpResults.Results&lt;,...,&gt;</c></param>
    /// <param name="mapper">The mapper to convert from command/query result to HTTP endpoint response model</param>
    /// <returns></returns>
    internal static TResults ToEndpointResponse<TResults, TEntity, TResponseModel>(this AppDomain.IResult result, Func<TEntity, TResponseModel> mapper)
    {
        var httpResult = result.Status switch
        {
            ResultStatus.Ok => result is AppDomain.Result ?
                TypedResults.Ok() :
                TypedResults.Ok(mapper((TEntity)result.GetValue())),
            ResultStatus.Created => TypedResults.Created("", mapper((TEntity)result.GetValue())),
            ResultStatus.NoContent => TypedResults.NoContent(),
            ResultStatus.NotFound => NotFoundEntity(result),
            ResultStatus.Unauthorized => UnAuthorized(result),
            ResultStatus.Forbidden => Forbidden(result),
            ResultStatus.Invalid => TypedResults.BadRequest(result.ValidationErrors),
            ResultStatus.Error => UnprocessableEntity(result),
            ResultStatus.Conflict => ConflictEntity(result),
            ResultStatus.Unavailable => UnavailableEntity(result),
            ResultStatus.CriticalError => CriticalEntity(result),
            _ => throw new NotSupportedException($"Result {result.Status} conversion is not supported."),
        };

        return (TResults)(dynamic)httpResult;
    }

    private static Microsoft.AspNetCore.Http.IResult UnprocessableEntity(AppDomain.IResult result)
    {
        var details = new StringBuilder("Next error(s) occurred:");

        foreach (var error in result.Errors) details.Append("* ").Append(error).AppendLine();

        return TypedResults.UnprocessableEntity(new ProblemDetails
        {
            Title = "Something went wrong.",
            Detail = details.ToString()
        });
    }

    private static Microsoft.AspNetCore.Http.IResult NotFoundEntity(AppDomain.IResult result)
    {
        var details = new StringBuilder("Next error(s) occurred:");

        if (result.Errors.Any())
        {
            foreach (var error in result.Errors) details.Append("* ").Append(error).AppendLine();

            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "Resource not found.",
                Detail = details.ToString()
            });
        }
        else
        {
            return TypedResults.NotFound();
        }
    }

    private static Microsoft.AspNetCore.Http.IResult ConflictEntity(AppDomain.IResult result)
    {
        var details = new StringBuilder("Next error(s) occurred:");

        if (result.Errors.Any())
        {
            foreach (var error in result.Errors) details.Append("* ").Append(error).AppendLine();

            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "There was a conflict.",
                Detail = details.ToString()
            });
        }
        else
        {
            return TypedResults.Conflict();
        }
    }

    private static Microsoft.AspNetCore.Http.IResult CriticalEntity(AppDomain.IResult result)
    {
        var details = new StringBuilder("Next error(s) occurred:");

        if (result.Errors.Any())
        {
            foreach (var error in result.Errors) details.Append("* ").Append(error).AppendLine();

            return TypedResults.Problem(new ProblemDetails()
            {
                Title = "Something went wrong.",
                Detail = details.ToString(),
                Status = StatusCodes.Status500InternalServerError
            });
        }
        else
        {
            return TypedResults.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static Microsoft.AspNetCore.Http.IResult UnavailableEntity(AppDomain.IResult result)
    {
        var details = new StringBuilder("Next error(s) occurred:");

        if (result.Errors.Any())
        {
            foreach (var error in result.Errors) details.Append("* ").Append(error).AppendLine();

            return TypedResults.Problem(new ProblemDetails
            {
                Title = "Service unavailable.",
                Detail = details.ToString(),
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
        else
        {
            return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static Microsoft.AspNetCore.Http.IResult Forbidden(AppDomain.IResult result)
    {
        var details = new StringBuilder("Next error(s) occurred:");

        if (result.Errors.Any())
        {
            foreach (var error in result.Errors) details.Append("* ").Append(error).AppendLine();

            return TypedResults.Problem(new ProblemDetails
            {
                Title = "Forbidden.",
                Detail = details.ToString(),
                Status = StatusCodes.Status403Forbidden
            });
        }
        else
        {
            return TypedResults.Forbid();
        }
    }

    private static Microsoft.AspNetCore.Http.IResult UnAuthorized(AppDomain.IResult result)
    {
        var details = new StringBuilder("Next error(s) occurred:");

        if (result.Errors.Any())
        {
            foreach (var error in result.Errors) details.Append("* ").Append(error).AppendLine();

            return TypedResults.Problem(new ProblemDetails
            {
                Title = "Unauthorized.",
                Detail = details.ToString(),
                Status = StatusCodes.Status401Unauthorized
            });
        }
        else
        {
            return TypedResults.Unauthorized();
        }
    }
}
#endif
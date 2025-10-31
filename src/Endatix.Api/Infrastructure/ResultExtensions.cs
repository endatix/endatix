using System.Text;
using Microsoft.AspNetCore.Mvc;
using Endatix.Core.Infrastructure.Result;
using AppDomain = Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Infrastructure;

#if NET7_0_OR_GREATER
public static partial class ResultExtensions
{
    private const string DEFAULT_NOT_FOUND_TITLE = "Resource not found.";

    private const string DEFAULT_UNEXPECTED_ERROR_TITLE = "An unexpected error occurred.";
    private const string DEFAULT_BAD_REQUEST_TITLE = "There was a problem with your request.";

    /// <summary>
    /// Converts an IResult from an operation to a NotFound HTTP IResult with ProblemDetails.
    /// </summary>
    /// <param name="result">The IResult to convert.</param>
    /// <returns>A NotFound HTTP IResult with ProblemDetails.</returns>
    public static NotFound<ProblemDetails> ToNotFound(this AppDomain.IResult result)
    {
        var details = new StringBuilder("Next error(s) occurred:");

        if (result.Errors.Any())
        {
            foreach (var error in result.Errors)
            {
                details.Append("* ").Append(error).AppendLine();
            }
        }
        else
        {
            details.Append("* ").Append(DEFAULT_NOT_FOUND_TITLE).AppendLine();
        }

        return TypedResults.NotFound(new ProblemDetails
        {
            Title = DEFAULT_NOT_FOUND_TITLE,
            Detail = details.ToString(),
            Status = StatusCodes.Status404NotFound
        });
    }

    /// <summary>
    /// Converts an IResult from an operation to a BadRequest HTTP IResult with ProblemDetails.
    /// This method constructs a BadRequest response with ProblemDetails, including a title and detail.
    /// The title defaults to "There was a problem with your request." if not provided.
    /// The detail includes a list of validation errors if present, otherwise, it defaults to the default title.
    /// </summary>
    /// <param name="result">The IResult to convert.</param>
    /// <param name="title">Optional title for the BadRequest response. Defaults to "There was a problem with your request."</param>
    /// <returns>A BadRequest HTTP IResult with ProblemDetails.</returns>
    public static BadRequest<ProblemDetails> ToBadRequest(this AppDomain.IResult result, string? title = null)
    {
        var details = DEFAULT_BAD_REQUEST_TITLE;
        var validationError = result.ValidationErrors.FirstOrDefault();
        if (validationError != null)
        {
            details = validationError.ErrorMessage;
        }

        var problemDetails = new ProblemDetails
        {
            Title = title ?? DEFAULT_BAD_REQUEST_TITLE,
            Detail = details.ToString(),
            Status = StatusCodes.Status400BadRequest
        };

        if (validationError?.ErrorCode != null)
        {
            problemDetails.Extensions.Add("errorCode", validationError.ErrorCode);
        }

        return TypedResults.BadRequest(problemDetails);
    }

    public static ProblemHttpResult ToProblem(this AppDomain.IResult result, string? title = null)
    {
        var status = result.Status switch
        {
            ResultStatus.Invalid => StatusCodes.Status400BadRequest,
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
            ResultStatus.Error => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

        var problemResult = TypedResults.Problem(
            title: title ?? DEFAULT_UNEXPECTED_ERROR_TITLE,
            statusCode: status);

        var details = new StringBuilder();

        foreach (var error in result.Errors)
        {
            details.Append(error).AppendLine();
        }

        if (result.IsInvalid())
        {
            problemResult.ProblemDetails.Title = title ?? DEFAULT_BAD_REQUEST_TITLE;
            foreach (var error in result.ValidationErrors)
            {
                details.Append(error.ErrorMessage).AppendLine();
            }

            var errorCode = result.ValidationErrors.FirstOrDefault()?.ErrorCode;
            if (errorCode != null)
            {
                problemResult.ProblemDetails.Extensions.Add("errorCode", errorCode);
            }
        }

        problemResult.ProblemDetails.Detail = details.ToString();

        return problemResult;
    }

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

        foreach (var error in result.Errors)
        {
            details.Append("* ").Append(error).AppendLine();
        }

        return TypedResults.UnprocessableEntity(new ProblemDetails
        {
            Title = DEFAULT_UNEXPECTED_ERROR_TITLE,
            Detail = details.ToString()
        });
    }

    private static Microsoft.AspNetCore.Http.IResult NotFoundEntity(AppDomain.IResult result)
    {
        var details = new StringBuilder("Next error(s) occurred:");

        if (result.Errors.Any())
        {
            foreach (var error in result.Errors)
            {
                details.Append("* ").Append(error).AppendLine();
            }

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
            foreach (var error in result.Errors)
            {
                details.Append("* ").Append(error).AppendLine();
            }

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
            foreach (var error in result.Errors)
            {
                details.Append("* ").Append(error).AppendLine();
            }

            return TypedResults.Problem(new ProblemDetails()
            {
                Title = DEFAULT_UNEXPECTED_ERROR_TITLE,
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
            foreach (var error in result.Errors)
            {
                details.Append("* ").Append(error).AppendLine();
            }

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
            foreach (var error in result.Errors)
            {
                details.Append("* ").Append(error).AppendLine();
            }

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
            foreach (var error in result.Errors)
            {
                details.Append("* ").Append(error).AppendLine();
            }

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
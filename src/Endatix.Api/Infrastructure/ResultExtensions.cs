using Microsoft.AspNetCore.Mvc;
using Endatix.Core.Infrastructure.Result;
using AppDomain = Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Infrastructure;

#if NET7_0_OR_GREATER
public static partial class ResultExtensions
{
    public static class ResultTitles
    {
        public const string NOT_FOUND = "Resource not found";
        public const string UNAUTHORIZED = "Unauthorized access";
        public const string FORBIDDEN = "Forbidden access";
        public const string CONFLICT = "There was a conflict";
        public const string BAD_REQUEST = "There was a problem with your request";
        public const string INTERNAL_SERVER_ERROR = "An unexpected error occurred";
        public const string SERVICE_UNAVAILABLE = "Service unavailable";
    }

    /// <summary>
    /// Converts an operation <see cref="AppDomain.IResult"/> to a RFC7807 <see cref="ProblemHttpResult"/>.
    /// Maps Invalid → 400, NotFound → 404, Conflict → 409, Unauthorized → 401, Forbidden → 403,
    /// Error/CriticalError → 500, Unavailable → 503.
    /// <c>detail</c> is always populated, falling back to the title when the result carries no errors.
    /// </summary>
    public static ProblemHttpResult ToProblem(this AppDomain.IResult result, string? title = null)
    {
        var (status, defaultTitle) = result.Status switch
        {
            ResultStatus.Invalid => (StatusCodes.Status400BadRequest, ResultTitles.BAD_REQUEST),
            ResultStatus.NotFound => (StatusCodes.Status404NotFound, ResultTitles.NOT_FOUND),
            ResultStatus.Conflict => (StatusCodes.Status409Conflict, ResultTitles.CONFLICT),
            ResultStatus.Unauthorized => (StatusCodes.Status401Unauthorized, ResultTitles.UNAUTHORIZED),
            ResultStatus.Forbidden => (StatusCodes.Status403Forbidden, ResultTitles.FORBIDDEN),
            ResultStatus.Error => (StatusCodes.Status500InternalServerError, ResultTitles.INTERNAL_SERVER_ERROR),
            ResultStatus.CriticalError => (StatusCodes.Status500InternalServerError, ResultTitles.INTERNAL_SERVER_ERROR),
            ResultStatus.Unavailable => (StatusCodes.Status503ServiceUnavailable, ResultTitles.SERVICE_UNAVAILABLE),
            // Ok/Created/NoContent must never reach here; treat as a server-side mapping fault.
            _ => (StatusCodes.Status500InternalServerError, ResultTitles.INTERNAL_SERVER_ERROR)
        };

        var resolvedTitle = title ?? defaultTitle;
        var problemResult = TypedResults.Problem(
            title: resolvedTitle,
            statusCode: status);

        var messages = new List<string>(result.Errors);

        if (result.IsInvalid())
        {
            messages.AddRange(result.ValidationErrors.Select(error => error.ErrorMessage));

            var errorCode = result.ValidationErrors.FirstOrDefault()?.ErrorCode;
            if (errorCode != null)
            {
                problemResult.ProblemDetails.Extensions.Add("errorCode", errorCode);
            }

            var fields = result.ValidationErrors
                .Where(error => !string.IsNullOrWhiteSpace(error.Identifier))
                .GroupBy(error => error.Identifier)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());

            if (fields.Count > 0)
            {
                problemResult.ProblemDetails.Extensions.Add("fields", fields);
            }
        }

        // `detail` is always populated - never an empty string. Consumers (Hub's
        // ProblemDetailsSchema included) treat it as a required, human-readable message,
        // so a result carrying no errors falls back to the title.
        var detail = string.Join(Environment.NewLine, messages.Where(message => !string.IsNullOrWhiteSpace(message)));
        problemResult.ProblemDetails.Detail = string.IsNullOrWhiteSpace(detail) ? resolvedTitle : detail;

        return problemResult;
    }
}
#endif

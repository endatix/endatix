using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using static Endatix.Api.Infrastructure.ResultExtensions;

namespace Endatix.Api.Tests.Infrastructure;

public class EndatixProblemDetailsTests
{
    [Fact]
    public void FromValidationFailures_WithPropertyErrors_ReturnsCanonicalProblemDetails()
    {
        // Arrange
        var httpContext = CreateHttpContext("/api/forms", "trace-abc");
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required.") { ErrorCode = "NotEmptyValidator" },
            new("Name", "Name must be at least 2 characters."),
            new("IsEnabled", "IsEnabled is required."),
        };

        // Act
        ProblemDetails problem = EndatixProblemDetails.FromValidationFailures(
            failures,
            httpContext,
            StatusCodes.Status400BadRequest);

        // Assert
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be(ResultTitles.BAD_REQUEST);
        problem.Type.Should().Be("https://www.rfc-editor.org/rfc/rfc9110.html#name-400-bad-request");
        problem.Instance.Should().Be("/api/forms");
        problem.Detail.Should().Contain("Name is required.");
        problem.Detail.Should().NotContain("\r");
        problem.Detail.Should().NotBeNullOrWhiteSpace();
        problem.Extensions.Should().ContainKey("traceId");
        problem.Extensions["traceId"].Should().Be("trace-abc");
        problem.Extensions.Should().ContainKey("errorCode");
        problem.Extensions["errorCode"].Should().Be("NotEmptyValidator");
        problem.Extensions.Should().ContainKey("fields");
        var fields = problem.Extensions["fields"].Should().BeOfType<Dictionary<string, string[]>>().Subject;
        fields.Should().ContainKey("Name");
        fields["Name"].Should().HaveCount(2);
        fields.Should().ContainKey("IsEnabled");
        httpContext.Response.ContentType.Should().Be("application/problem+json");
    }

    [Fact]
    public void FromValidationFailures_WithEmptyFailures_FallsBackDetailToTitle()
    {
        // Arrange
        var httpContext = CreateHttpContext("/api/forms", "trace-empty");

        // Act
        ProblemDetails problem = EndatixProblemDetails.FromValidationFailures(
            [],
            httpContext,
            StatusCodes.Status400BadRequest);

        // Assert
        problem.Detail.Should().Be(ResultTitles.BAD_REQUEST);
        problem.Extensions.Should().NotContainKey("fields");
        problem.Extensions.Should().NotContainKey("errorCode");
    }

    [Fact]
    public void Create_WithoutHttpContext_StillReturnsStatusAndDetail()
    {
        // Act
        ProblemDetails problem = EndatixProblemDetails.Create(
            statusCode: StatusCodes.Status404NotFound,
            title: null,
            detail: "Form not found");

        // Assert
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be(ResultTitles.NOT_FOUND);
        problem.Detail.Should().Be("Form not found");
        problem.Type.Should().Be("https://www.rfc-editor.org/rfc/rfc9110.html#name-404-not-found");
    }

    [Fact]
    public void ForUnhandledException_DoesNotIncludeExceptionText()
    {
        // Arrange
        var httpContext = CreateHttpContext("/api/boom", "trace-500");

        // Act
        ProblemDetails problem = EndatixProblemDetails.ForUnhandledException(httpContext);

        // Assert
        problem.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Title.Should().Be(ResultTitles.INTERNAL_SERVER_ERROR);
        problem.Detail.Should().Be(ResultTitles.INTERNAL_SERVER_ERROR);
        problem.Instance.Should().Be("/api/boom");
        problem.Extensions["traceId"].Should().Be("trace-500");
    }

    [Fact]
    public void ToProblem_WithHttpContextAccessor_IncludesTypeInstanceTraceId()
    {
        // Arrange
        var httpContext = CreateHttpContext("/api/forms/99", "trace-to-problem");
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        EndatixProblemDetails.Configure(accessor);

        var result = Result.NotFound("Form not found");

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        httpResult.ProblemDetails.Type.Should().Be("https://www.rfc-editor.org/rfc/rfc9110.html#name-404-not-found");
        httpResult.ProblemDetails.Instance.Should().Be("/api/forms/99");
        httpResult.ProblemDetails.Extensions.Should().ContainKey("traceId");
        httpResult.ProblemDetails.Extensions["traceId"].Should().Be("trace-to-problem");
    }

    [Fact]
    public void ToProblem_WithoutHttpContext_DoesNotThrow()
    {
        // Arrange — clear accessor by configuring a null-context accessor
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        EndatixProblemDetails.Configure(accessor);

        var result = Result.Invalid(new ValidationError("Name", "Name is required.", "NotEmptyValidator", ValidationSeverity.Error));

        // Act
        var act = () => result.ToProblem();

        // Assert
        act.Should().NotThrow();
        var httpResult = act();
        httpResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        httpResult.ProblemDetails.Detail.Should().Contain("Name is required.");
        httpResult.ProblemDetails.Extensions.Should().ContainKey("errorCode");
        httpResult.ProblemDetails.Extensions.Should().ContainKey("fields");
    }

    #region Security and Privacy Tests

    /// <summary>
    /// Handlers wrap `ex.Message` into Result.Error(...) in ~17 places. A 5xx body must never
    /// echo it back — DB/EF text, file paths and provider errors would reach the caller.
    /// </summary>
    [Theory]
    [InlineData(ResultStatus.Error, StatusCodes.Status500InternalServerError)]
    [InlineData(ResultStatus.CriticalError, StatusCodes.Status500InternalServerError)]
    [InlineData(ResultStatus.Unavailable, StatusCodes.Status503ServiceUnavailable)]
    public void ToProblem_ServerError_DoesNotEchoHandlerSuppliedDetail(ResultStatus status, int expectedStatusCode)
    {
        // Arrange
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(CreateHttpContext("/api/folders/1", "trace-5xx"));
        EndatixProblemDetails.Configure(accessor);

        const string leak = "Error deleting folder: 23503 violates foreign key \"FK_Forms_Folders\" on table forms";
        Core.Infrastructure.Result.IResult result = status switch
        {
            ResultStatus.CriticalError => Result.CriticalError(leak),
            ResultStatus.Unavailable => Result.Unavailable(leak),
            _ => Result.Error(leak),
        };

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.StatusCode.Should().Be(expectedStatusCode);
        httpResult.ProblemDetails.Detail.Should().NotContain("foreign key");
        httpResult.ProblemDetails.Detail.Should().NotContain("FK_Forms_Folders");
        httpResult.ProblemDetails.Detail.Should().Be(httpResult.ProblemDetails.Title);
    }

    /// <summary>
    /// 4xx detail is author-written and user-actionable, so it must still reach the client.
    /// </summary>
    [Fact]
    public void ToProblem_ClientError_KeepsDetail()
    {
        // Arrange
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(CreateHttpContext("/api/forms/9", "trace-404"));
        EndatixProblemDetails.Configure(accessor);

        // Act
        var httpResult = Result.NotFound("Form not found.").ToProblem();

        // Assert
        httpResult.ProblemDetails.Detail.Should().Be("Form not found.");
    }

    [Fact]
    public void Create_ServerError_SuppressesDetailForExportAndOtherDirectCallers()
    {
        // Act
        var problem = EndatixProblemDetails.Create(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Export failed",
            detail: "Export failed: Object reference not set to an instance of an object.",
            httpContext: CreateHttpContext("/api/forms/1/submissions/export", "trace-export"));

        // Assert
        problem.Detail.Should().Be("Export failed");
        problem.Detail.Should().NotContain("Object reference");
    }

    [Fact]
    public void TitleForStatus_RateLimit_DoesNotUseInternalServerErrorTitle()
    {
        EndatixProblemDetails.TitleForStatus(StatusCodes.Status429TooManyRequests)
            .Should().Be(ResultTitles.TOO_MANY_REQUESTS);
        EndatixProblemDetails.TitleForStatus(StatusCodes.Status422UnprocessableEntity)
            .Should().Be(ResultTitles.BAD_REQUEST);
    }

    #endregion

    private static DefaultHttpContext CreateHttpContext(string path, string traceId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.TraceIdentifier = traceId;
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }
}

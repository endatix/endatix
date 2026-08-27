using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using static Endatix.Api.Infrastructure.ResultExtensions;

namespace Endatix.Api.Tests.Infrastructure;

public class ResultExtensionsTests
{
    private const string DEFAULT_UNEXPECTED_ERROR_TITLE = ResultTitles.INTERNAL_SERVER_ERROR;
    private const string DEFAULT_BAD_REQUEST_TITLE = ResultTitles.BAD_REQUEST;
    private const string DEFAULT_UNAUTHORIZED_TITLE = ResultTitles.UNAUTHORIZED;
    private const string DEFAULT_FORBIDDEN_TITLE = ResultTitles.FORBIDDEN;
    private const string DEFAULT_CONFLICT_TITLE = ResultTitles.CONFLICT;
    private const string DEFAULT_SERVICE_UNAVAILABLE_TITLE = ResultTitles.SERVICE_UNAVAILABLE;

    [Theory]
    [InlineData(ResultStatus.Invalid, StatusCodes.Status400BadRequest)]
    [InlineData(ResultStatus.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ResultStatus.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ResultStatus.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ResultStatus.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ResultStatus.Error, StatusCodes.Status500InternalServerError)]
    [InlineData(ResultStatus.Unavailable, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(ResultStatus.CriticalError, StatusCodes.Status500InternalServerError)]
    public void ToProblem_WithDifferentStatuses_ReturnsCorrectHttpStatusCode(ResultStatus status, int expectedStatusCode)
    {
        // Arrange
        var result = CreateResultWithStatus(status, "Error message");

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public void ToProblem_WithErrors_ReturnsProblemDetailsWithErrors()
    {
        // Arrange
        var result = Result.Error("First error");

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void ToProblem_WithValidationError_ReturnsProblemDetailsWithValidationError()
    {
        // Arrange
        var validationError = new ValidationError("Field", "Validation error message", "ERROR_CODE", ValidationSeverity.Error);
        var result = Result.Invalid(validationError);

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ToProblem_WithCustomTitle_ReturnsProblemDetailsWithCustomTitle()
    {
        // Arrange
        var result = Result.Error("Error occurred");

        // Act
        var httpResult = result.ToProblem("Custom error title");

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void ToProblem_WithErrorResult_IgnoresValidationErrors()
    {
        // Arrange
        var result = Result.Error("General error");
        // Add validation errors through reflection since they're protected
        var resultType = result.GetType();
        var validationErrorsProperty = resultType.GetProperty("ValidationErrors");
        var validationError = new ValidationError("Field", "Validation error", "ERROR_CODE", ValidationSeverity.Error);
        validationErrorsProperty?.SetValue(result, new[] { validationError });

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var problemDetails = httpResult.ProblemDetails;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be(DEFAULT_UNEXPECTED_ERROR_TITLE);
        problemDetails.Detail.Should().Contain("General error");
        problemDetails.Extensions.Should().NotContainKey("errorCode");
    }

    [Fact]
    public void ToProblem_WithValidationError_ReturnsProblemDetailsCorectly()
    {
        // Arrange
        var result = Result.Invalid(new ValidationError("Field", "Validation error", "ERROR_CODE", ValidationSeverity.Error));

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problemDetails = httpResult.ProblemDetails;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be(DEFAULT_BAD_REQUEST_TITLE);
        problemDetails.Detail.Should().Contain("Validation error");
        problemDetails.Extensions.Should().ContainKey("errorCode");
        problemDetails.Extensions["errorCode"].Should().Be("ERROR_CODE");
        problemDetails.Extensions.Should().ContainKey("fields");
        var fields =
            problemDetails.Extensions["fields"].Should().BeOfType<Dictionary<string, string[]>>().Subject;
        fields.Should().ContainKey("Field");
        fields["Field"].Should().ContainSingle("Validation error");
    }

    [Fact]
    public void ToProblem_WithUnsupportedStatus_ReturnsInternalServerError()
    {
        // Arrange
        var result = CreateResultWithStatus(ResultStatus.Ok, "Success message");

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var problemDetails = httpResult.ProblemDetails;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be(DEFAULT_UNEXPECTED_ERROR_TITLE);
        problemDetails.Detail.Should().Be(DEFAULT_UNEXPECTED_ERROR_TITLE);
    }

    [Fact]
    public void ToProblem_Unauthorized_UsesUnauthorizedDefaultTitle()
    {
        // Arrange
        var result = Result.Unauthorized("Unauthorized");

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.ProblemDetails.Title.Should().Be(DEFAULT_UNAUTHORIZED_TITLE);
        httpResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void ToProblem_Forbidden_UsesForbiddenDefaultTitle()
    {
        // Arrange
        var result = Result.Forbidden("Forbidden");

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.ProblemDetails.Title.Should().Be(DEFAULT_FORBIDDEN_TITLE);
        httpResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void ToProblem_Conflict_UsesConflictDefaultTitle()
    {
        // Arrange
        var result = Result.Conflict("Duplicate submission");

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.ProblemDetails.Title.Should().Be(DEFAULT_CONFLICT_TITLE);
        httpResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void ToProblem_Unavailable_UsesServiceUnavailableDefaultTitle()
    {
        // Arrange
        var result = Result.Unavailable("Email provider is not configured.");

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.ProblemDetails.Title.Should().Be(DEFAULT_SERVICE_UNAVAILABLE_TITLE);
        httpResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public void ToProblem_WithEmptyErrors_ReturnsProblemDetailsWithDefaultTitle()
    {
        // Arrange
        var result = Result.Error();

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var problemDetails = httpResult.ProblemDetails;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be(DEFAULT_UNEXPECTED_ERROR_TITLE);
        problemDetails.Detail.Should().Be(DEFAULT_UNEXPECTED_ERROR_TITLE);
    }

    [Fact]
    public void ToProblem_WithDefaultTitle_UsesDefaultUnexpectedErrorTitle()
    {
        // Arrange
        var result = Result.Error("Error occurred");

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var problemDetails = httpResult.ProblemDetails;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be(DEFAULT_UNEXPECTED_ERROR_TITLE);
        problemDetails.Detail.Should().Contain("Error occurred");
    }

    [Fact]
    public void ToProblem_WithCustomTitle_UsesProvidedTitle()
    {
        // Arrange
        var result = Result.Error("Error occurred");
        var customTitle = "Custom error title";

        // Act
        var httpResult = result.ToProblem(customTitle);

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var problemDetails = httpResult.ProblemDetails;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be(customTitle);
        problemDetails.Detail.Should().Contain("Error occurred");
    }

    [Fact]
    public void ToProblem_WithMultipleErrors_CombinesAllErrorMessages()
    {
        // Arrange
        var result = Result.Error(new ErrorList(["First error", "Second error"]));

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var problemDetails = httpResult.ProblemDetails;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be(DEFAULT_UNEXPECTED_ERROR_TITLE);
        problemDetails.Detail.Should().Contain("First error");
        problemDetails.Detail.Should().Contain("Second error");
    }

    [Fact]
    public void ToProblem_WithMultipleValidationErrors_CombinesAllValidationErrorMessages()
    {
        // Arrange
        var validationError1 = new ValidationError("Field1", "First validation error", "ERROR1", ValidationSeverity.Error);
        var validationError2 = new ValidationError("Field2", "Second validation error", "ERROR2", ValidationSeverity.Error);
        var result = Result.Invalid(validationError1, validationError2);

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problemDetails = httpResult.ProblemDetails;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }

        problemDetails.Title.Should().Be(DEFAULT_BAD_REQUEST_TITLE);
        problemDetails.Detail.Should().Contain("First validation error");
        problemDetails.Detail.Should().Contain("Second validation error");
        problemDetails.Extensions.Should().ContainKey("errorCode");
        problemDetails.Extensions["errorCode"].Should().Be("ERROR1");
    }

    [Fact]
    public void ToProblem_WithEmptyResult_HandlesGracefully()
    {
        // Arrange
        var result = Result.Success<string>(null!);

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<ProblemHttpResult>();

        var statusCode = httpResult.StatusCode;
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var problemDetails = httpResult.ProblemDetails;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be(DEFAULT_UNEXPECTED_ERROR_TITLE);
        problemDetails.Detail.Should().Be(DEFAULT_UNEXPECTED_ERROR_TITLE);
    }

    [Fact]
    public void ToProblem_NotFoundWithoutErrors_FallsBackToTitleForDetail()
    {
        // Arrange
        var result = Result.NotFound();

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        httpResult.ProblemDetails.Title.Should().Be(ResultTitles.NOT_FOUND);
        httpResult.ProblemDetails.Detail.Should().Be(ResultTitles.NOT_FOUND);
    }

    [Fact]
    public void ToProblem_WithErrors_DoesNotPadDetailWithWhitespace()
    {
        // Arrange
        var result = Result.NotFound("Form not found.");

        // Act
        var httpResult = result.ToProblem();

        // Assert
        httpResult.ProblemDetails.Detail.Should().Be("Form not found.");
    }

    private static Core.Infrastructure.Result.IResult CreateResultWithStatus(ResultStatus status, params string[] errors)
    {
        return status switch
        {
            ResultStatus.Invalid => Result.Invalid(new ValidationError("Field", "Validation error", "ERROR_CODE", ValidationSeverity.Error)),
            ResultStatus.NotFound => Result.NotFound(errors),
            ResultStatus.Conflict => Result.Conflict(errors),
            ResultStatus.Unauthorized => Result.Unauthorized(errors),
            ResultStatus.Forbidden => Result.Forbidden(errors),
            ResultStatus.Error => Result.Error(errors.FirstOrDefault() ?? "Error occurred"),
            ResultStatus.Unavailable => Result.Unavailable(errors),
            ResultStatus.CriticalError => Result.CriticalError(errors),
            ResultStatus.Ok => Result.Success("Success"),
            _ => Result.Error("Unknown status")
        };
    }
}

using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Endatix.Api.Tests.Infrastructure;

public class ResultExtensionsTests
{
    internal const string DEFAULT_UNEXPECTED_ERROR_TITLE = "An unexpected error occurred.";
    internal const string DEFAULT_BAD_REQUEST_TITLE = "There was a problem with your request.";

    [Fact]
    public void ToNotFound_WithErrors_ReturnsProblemDetailsWithErrors()
    {
        // Arrange
        var result = Result.NotFound("Resource not found", "Additional error");

        // Act
        var httpResult = result.ToNotFound();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<NotFound<ProblemDetails>>();

        var problemDetails = httpResult.Value;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be("Resource not found.");
        problemDetails.Status.Should().Be(StatusCodes.Status404NotFound);
        problemDetails.Detail.Should().Contain("Resource not found");
        problemDetails.Detail.Should().Contain("Additional error");
    }

    [Fact]
    public void ToNotFound_WithoutErrors_ReturnsProblemDetailsWithDefaultMessage()
    {
        // Arrange
        var result = Result.NotFound();

        // Act
        var httpResult = result.ToNotFound();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<NotFound<ProblemDetails>>();

        var problemDetails = httpResult.Value;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be("Resource not found.");
        problemDetails.Status.Should().Be(StatusCodes.Status404NotFound);
        problemDetails.Detail.Should().Contain("Resource not found.");
    }

    [Fact]
    public void ToBadRequest_WithValidationError_ReturnsProblemDetailsWithValidationError()
    {
        // Arrange
        var validationError = new ValidationError("Field", "Invalid field value", "INVALID_FIELD", ValidationSeverity.Error);
        var result = Result.Invalid(validationError);

        // Act
        var httpResult = result.ToBadRequest();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<BadRequest<ProblemDetails>>();

        var problemDetails = httpResult.Value;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be("There was a problem with your request.");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be("Invalid field value");
        problemDetails.Extensions.Should().ContainKey("errorCode");
        problemDetails.Extensions["errorCode"].Should().Be("INVALID_FIELD");
    }

    [Fact]
    public void ToBadRequest_WithCustomTitle_ReturnsProblemDetailsWithCustomTitle()
    {
        // Arrange
        var validationError = new ValidationError("Field", "Invalid field value", "ERROR_CODE", ValidationSeverity.Error);
        var result = Result.Invalid(validationError);
        var customTitle = "Custom validation error";

        // Act
        var httpResult = result.ToBadRequest(customTitle);

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<BadRequest<ProblemDetails>>();

        var problemDetails = httpResult.Value;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be(customTitle);
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ToBadRequest_WithoutValidationError_ReturnsProblemDetailsWithDefaultMessage()
    {
        // Arrange
        var result = Result.Error("Some error occurred");

        // Act
        var httpResult = result.ToBadRequest();

        // Assert
        httpResult.Should().NotBeNull();
        httpResult.Should().BeOfType<BadRequest<ProblemDetails>>();

        var problemDetails = httpResult.Value;
        if (problemDetails is null)
        {
            Assert.Fail("Problem details are null");
        }
        problemDetails.Title.Should().Be("There was a problem with your request.");
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Detail.Should().Be("There was a problem with your request.");
    }

    [Theory]
    [InlineData(ResultStatus.Invalid, StatusCodes.Status400BadRequest)]
    [InlineData(ResultStatus.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ResultStatus.Error, StatusCodes.Status500InternalServerError)]
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
    public void ToProblem_WithErrorResult_ReturnsProblemDetailsCorrectly()
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
        problemDetails.Detail.Should().BeEmpty();
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
        problemDetails.Detail.Should().BeEmpty();
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
        var result = Result.Error("First error");

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
        problemDetails.Detail.Should().BeEmpty();
    }

    private static Core.Infrastructure.Result.IResult CreateResultWithStatus(ResultStatus status, params string[] errors)
    {
        return status switch
        {
            ResultStatus.Invalid => Result.Invalid(new ValidationError("Field", "Validation error", "ERROR_CODE", ValidationSeverity.Error)),
            ResultStatus.NotFound => Result.NotFound(errors),
            ResultStatus.Error => Result.Error(errors.FirstOrDefault() ?? "Error occurred"),
            ResultStatus.Ok => Result.Success("Success"),
            _ => Result.Error("Unknown status")
        };
    }
}

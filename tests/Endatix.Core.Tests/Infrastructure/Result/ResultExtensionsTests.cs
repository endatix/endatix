using Endatix.Core.Infrastructure.Result;
using CoreResult = Endatix.Core.Infrastructure.Result.Result;

namespace Endatix.Core.Tests.Infrastructure.Result;

public sealed class ResultExtensionsTests
{
    [Fact]
    public void ToErrorResult_FromGenericErrorResult_MapsToDestinationType()
    {
        // Arrange
        var source = Result<int>.NotFound("missing");

        // Act
        var destination = source.ToErrorResult<string>();

        // Assert
        destination.Status.Should().Be(ResultStatus.NotFound);
        destination.Errors.Should().ContainSingle().Which.Should().Be("missing");
    }

    [Fact]
    public void ToErrorResult_FromIResultPreservesCorrelationId_ForErrorStatus()
    {
        // Arrange
        const string correlationId = "corr-123";
        IResult source = CoreResult.Error(new ErrorList(["boom"], correlationId));

        // Act
        var destination = source.ToErrorResult<int>();

        // Assert
        destination.Status.Should().Be(ResultStatus.Error);
        destination.CorrelationId.Should().Be(correlationId);
        destination.Errors.Should().ContainSingle().Which.Should().Be("boom");
    }

    [Fact]
    public void ToErrorResult_FromValidationResult_PreservesValidationErrors()
    {
        // Arrange
        var validationError = new ValidationError("Field", "Invalid value", "INVALID", ValidationSeverity.Error);
        var source = Result<Guid>.Invalid(validationError);

        // Act
        var destination = source.ToErrorResult<long>();

        // Assert
        destination.Status.Should().Be(ResultStatus.Invalid);
        destination.ValidationErrors.Should().ContainSingle();
        destination.ValidationErrors.First().ErrorMessage.Should().Be("Invalid value");
    }

    [Fact]
    public void ToErrorResult_FromSuccessResult_ThrowsInvalidOperationException()
    {
        // Arrange
        var source = Result<int>.Success(42);

        // Act
        var action = () => source.ToErrorResult<string>();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot convert to error result*");
    }

    [Fact]
    public void Map_FromErrorResult_UsesErrorConversionForDestination()
    {
        // Arrange
        var source = Result<int>.Forbidden("not-allowed");

        // Act
        var destination = source.Map(static value => value.ToString());

        // Assert
        destination.Status.Should().Be(ResultStatus.Forbidden);
        destination.Errors.Should().ContainSingle().Which.Should().Be("not-allowed");
    }
}

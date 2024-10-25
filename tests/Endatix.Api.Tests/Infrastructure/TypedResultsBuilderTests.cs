using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Infrastructure;

public class TypedResultsBuilderTests
{
    [Fact]
    public void FromResult_ValidInput_SetsResult()
    {
        // Arrange
        var result = Result.Success("foo");

        // Act
        var resultsBuilder = TypedResultsBuilder.FromResult(result);

        // Assert
        resultsBuilder.Should().NotBeNull();
        resultsBuilder.Should().BeOfType<TypedResultsBuilder<string>>();
        resultsBuilder.SourceResult.Status.Should().Be(ResultStatus.Ok);
        resultsBuilder.SourceResult.Value.Should().Be("foo");
    }

    [Fact]
    public void FromResult_NoResultPassed_Throws()
    {
        // Arrange
        Result<BadRequest>? nullResult = null;

        // Act
        var action = () => TypedResultsBuilder.FromResult(nullResult);

        //Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapResult_WithMappingFunction_SetsCorrectDestinationType()
    {
        // Arrange
        var result = Result.Success(new Source(1));
        Func<Source, Destination> mappingFunc = _ => new Destination("1");

        // Act
        var resultsBuilder = TypedResultsBuilder.MapResult(result, mappingFunc);

        //Assert
        resultsBuilder.Should().NotBeNull();
        resultsBuilder.Should().BeOfType<TypedResultsBuilder<Destination>>();
        resultsBuilder.SourceResult.Status.Should().Be(ResultStatus.Ok);
        resultsBuilder.SourceResult.Value.Should().Be(new Destination("1"));
    }

    [Fact]
    public void MapResult_WithNoMappingFunctionPassed_Throws()
    {
        // Arrange
        var result = Result.Success(new Source(1));
        Func<Source, Destination>? mappingFunc = null;

        // Act
        var action = () => TypedResultsBuilder.MapResult(result, mappingFunc);

        //Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapResult_NoResultPassed_Throws()
    {
        // Arrange
        Result<Source> nullResult = null;
        Func<Source, Destination> mappingFunc = _ => new Destination("1");

        // Act
        var action = () => TypedResultsBuilder.MapResult(nullResult, mappingFunc);

        //Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapResult_WithExceptionDuringMapping_Throws()
    {
        // Arrange
        var result = Result.Success(new Source(1));
        Func<Source, Destination>? mappingFunc = _ => throw new NullReferenceException("Exception was thrown during mapping");

        // Act
        var action = () => TypedResultsBuilder.MapResult(result, mappingFunc);

        //Assert
        action.Should().Throw<NullReferenceException>().WithMessage("Exception was thrown during mapping");
    }

    [Theory]
    [MemberData(nameof(TestResultsCombinations.SuccessAndInvalidResults), MemberType = typeof(TestResultsCombinations))]
    public void ConfigureResults_MatchedOkAndBadRequestSignature_PickedByImplicitCast(Result<Source> result)
    {
        // Arrange
        var resultsBuilder = TypedResultsBuilder
            .FromResult(result);

        // Act
        Results<Ok<Source>, BadRequest> httpResult = resultsBuilder.SetTypedResults<Ok<Source>, BadRequest>();

        // Assert
        Assert.IsType<Results<Ok<Source>, BadRequest>>(httpResult);
    }

    [Fact]
    public void ConfigureResults_UnMatchedOkAndBadRequestSignature_PickedByImplicitCastAndThrows()
    {
        // Arrange
        Result<Source> result = Result.NotFound();
        var resultsBuilder = TypedResultsBuilder
            .FromResult(result);

        // Act
        var action = () =>
        {
            Results<Ok<Source>, BadRequest> httpResult = resultsBuilder.SetTypedResults<Ok<Source>, BadRequest>();
        };

        // Assert
        action.Should().Throw<InvalidCastException>();
    }

    public record Source(int Data) { }

    public record Destination(string Data) { }

    public class TestResultsCombinations
    {
        public static IEnumerable<object[]> SuccessAndInvalidResults =>
            new List<object[]>{
                new object[]{Result.Success(new Source(default)) },
                new object[]{Result.Invalid(new ValidationError()) }
        };
    }
}
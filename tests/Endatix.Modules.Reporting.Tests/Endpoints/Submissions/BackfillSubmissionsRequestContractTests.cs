using System.Text.Json;
using Endatix.Framework.Serialization;
using Endatix.Modules.Reporting.Endpoints.Submissions;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Submissions;

/// <summary>
/// Pins the <c>completionScope</c> wire contract the Hub sends: lowercase strings, omitted means
/// completed, and anything else is rejected rather than silently falling back to completed.
/// </summary>
public sealed class BackfillSubmissionsRequestContractTests
{
    // FastEndpoints: web defaults plus the converter added in ApiApplicationBuilderExtensions.
    private static readonly JsonSerializerOptions _fastEndpointsLikeOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LongToStringConverter() },
    };

    [Theory]
    [InlineData("""{"completionScope":"incomplete"}""", SubmissionBackfillCompletion.Incomplete)]
    [InlineData("""{"completionScope":"completed"}""", SubmissionBackfillCompletion.Completed)]
    [InlineData("""{"completionScope":"Incomplete"}""", SubmissionBackfillCompletion.Incomplete)]
    public void Deserialize_ReadsWireString(string json, SubmissionBackfillCompletion expected)
    {
        // Act
        var request =
            JsonSerializer.Deserialize<BackfillSubmissionsRequest>(json, _fastEndpointsLikeOptions);

        // Assert
        request!.CompletionScope.Should().Be(expected);
    }

    [Fact]
    public void Deserialize_WhenOmitted_LeavesScopeUnset()
    {
        // Act
        var request = JsonSerializer.Deserialize<BackfillSubmissionsRequest>(
            """{"batchSize":100,"force":false}""",
            _fastEndpointsLikeOptions);

        // Assert
        request!.CompletionScope.Should().BeNull("the endpoint maps an omitted scope to completed");
    }

    [Fact]
    public void Deserialize_WithUnknownString_Throws()
    {
        // Act
        Action act = () => JsonSerializer.Deserialize<BackfillSubmissionsRequest>(
            """{"completionScope":"drafts"}""",
            _fastEndpointsLikeOptions);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData(SubmissionBackfillCompletion.Completed, "completed")]
    [InlineData(SubmissionBackfillCompletion.Incomplete, "incomplete")]
    public void Serialize_WritesLowercaseWireString(SubmissionBackfillCompletion completion, string expected)
    {
        // Act
        var json = JsonSerializer.Serialize(completion, _fastEndpointsLikeOptions);

        // Assert
        json.Should().Be($"\"{expected}\"");
    }

    [Fact]
    public void Validator_RejectsUndefinedNumericScope()
    {
        // Arrange: JsonStringEnumConverter still accepts numbers, so 7 binds.
        var request = JsonSerializer.Deserialize<BackfillSubmissionsRequest>(
            """{"formId":"100","completionScope":7}""",
            _fastEndpointsLikeOptions);

        // Act
        var result = new BackfillSubmissionsValidator().Validate(request!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == nameof(BackfillSubmissionsRequest.CompletionScope));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(SubmissionBackfillCompletion.Completed)]
    [InlineData(SubmissionBackfillCompletion.Incomplete)]
    public void Validator_AcceptsKnownOrOmittedScope(SubmissionBackfillCompletion? completion)
    {
        // Arrange
        BackfillSubmissionsRequest request = new() { FormId = 100, CompletionScope = completion };

        // Act
        var result = new BackfillSubmissionsValidator().Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

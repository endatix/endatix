using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

/// <summary>
/// Pins the legacy <c>submittedBy</c> translation of the obsolete constructor while it still ships.
/// Delete this file together with the constructor.
/// </summary>
#pragma warning disable CS0618
public class SubmissionObsoleteConstructorTests
{
    [Fact]
    public void Constructor_WhenSubmittedByIsNumeric_ForwardsSubmitterAndRestrictionGate()
    {
        // Act
        var submission = new Submission(
            SampleData.TENANT_ID,
            SampleData.SUBMISSION_JSON_DATA_1,
            formId: 123,
            formDefinitionId: 456,
            submittedBy: "789",
            enforceSingleSubmissionGate: true);

        // Assert
        submission.SubmitterId.Should().Be(789);
        submission.SubmitterDisplayId.Should().BeNull();
        submission.SubmittedBy.Should().Be("789");
        submission.RestrictionKey.Should().Be("SingleSubmission:Form:123:Submitter:789");
    }

    [Fact]
    public void Constructor_WhenSubmittedByIsOpaqueString_MapsToSubmitterDisplayId()
    {
        // Act
        var submission = new Submission(
            SampleData.TENANT_ID,
            SampleData.SUBMISSION_JSON_DATA_1,
            formId: 123,
            formDefinitionId: 456,
            submittedBy: "user123");

        // Assert
        submission.SubmitterId.Should().BeNull();
        submission.SubmitterDisplayId.Should().Be("user123");
        submission.SubmittedBy.Should().Be("user123");
        submission.RestrictionKey.Should().BeNull();
    }
}
#pragma warning restore CS0618

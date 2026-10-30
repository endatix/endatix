using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class SubmissionFreezeForTestTests
{
    private const string SNAPSHOT = """{"source":"test"}""";

    [Fact]
    public void FreezeForTest_TestSubmission_StoresTheSnapshot()
    {
        // Arrange
        var submission = NewSubmission(isTestSubmission: true);

        // Act
        submission.FreezeForTest(SNAPSHOT);

        // Assert
        submission.PersonalizationSnapshot.Should().Be(SNAPSHOT);
    }

    [Fact]
    public void FreezeForTest_RealSubmission_Throws()
    {
        // Arrange
        var submission = NewSubmission(isTestSubmission: false);

        // Act
        var freeze = () => submission.FreezeForTest(SNAPSHOT);

        // Assert
        freeze.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FreezeForTest_AlreadyFrozen_ThrowsAndKeepsTheFirst()
    {
        // Arrange
        var submission = NewSubmission(isTestSubmission: true);
        submission.FreezeForTest(SNAPSHOT);

        // Act
        var freeze = () => submission.FreezeForTest("""{"source":"other"}""");

        // Assert
        freeze.Should().Throw<InvalidOperationException>();
        submission.PersonalizationSnapshot.Should().Be(SNAPSHOT);
    }

    private static Submission NewSubmission(bool isTestSubmission) =>
        Submission.Create(new SubmissionCreateArgs(
            SampleData.TENANT_ID,
            FormId: 10,
            FormDefinitionId: 20,
            JsonData: "{}",
            IsTestSubmission: isTestSubmission));
}

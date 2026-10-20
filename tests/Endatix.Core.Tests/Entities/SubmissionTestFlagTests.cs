using Endatix.Core.Entities;
using Endatix.Core.Events;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

public class SubmissionTestFlagTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;
    private const long SubmitterId = 300;
    private const string RestrictionKey = "SingleSubmission:Form:100:Submitter:300";

    private static Submission Create(bool isTest, long? submitterId = SubmitterId) =>
        Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: FormDefinitionId,
            JsonData: "{}",
            IsComplete: false,
            SubmitterId: submitterId,
            IsTestSubmission: isTest,
            EnforceSingleSubmissionGate: true));

    [Fact]
    public void MarkAsTest_RealSubmission_FlagsTestAndClearsRestrictionKey()
    {
        // Arrange
        var submission = Create(isTest: false);
        submission.RestrictionKey.Should().Be(RestrictionKey);

        // Act
        submission.MarkAsTest();

        // Assert
        submission.IsTestSubmission.Should().BeTrue();
        submission.RestrictionKey.Should().BeNull();
    }

    [Fact]
    public void MarkAsTest_RealSubmission_RaisesUpdatedWithTestKindAndBumpsRevision()
    {
        // Arrange
        var submission = Create(isTest: false);
        var revision = submission.Revision;

        // Act
        submission.MarkAsTest();

        // Assert
        submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().ContainSingle()
            .Which.ChangeKind.Should().Be(SubmissionChangeKinds.Test);
        submission.Revision.Should().Be(revision + 1);
    }

    [Fact]
    public void MarkAsTest_AlreadyTest_DoesNothing()
    {
        // Arrange
        var submission = Create(isTest: true);
        var revision = submission.Revision;

        // Act
        submission.MarkAsTest();

        // Assert
        submission.IsTestSubmission.Should().BeTrue();
        submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().BeEmpty();
        submission.Revision.Should().Be(revision);
    }

    [Fact]
    public void MarkAsReal_WithGate_RestoresRestrictionKeyAndRaisesTestKind()
    {
        // Arrange
        var submission = Create(isTest: true);
        submission.RestrictionKey.Should().BeNull();

        // Act
        submission.MarkAsReal(enforceSingleSubmissionGate: true);

        // Assert
        submission.IsTestSubmission.Should().BeFalse();
        submission.RestrictionKey.Should().Be(RestrictionKey);
        submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().ContainSingle()
            .Which.ChangeKind.Should().Be(SubmissionChangeKinds.Test);
    }

    [Fact]
    public void MarkAsReal_WithoutGate_LeavesRestrictionKeyEmpty()
    {
        // Arrange
        var submission = Create(isTest: true);

        // Act
        submission.MarkAsReal(enforceSingleSubmissionGate: false);

        // Assert
        submission.IsTestSubmission.Should().BeFalse();
        submission.RestrictionKey.Should().BeNull();
    }

    [Fact]
    public void MarkAsReal_AlreadyReal_DoesNothing()
    {
        // Arrange
        var submission = Create(isTest: false);
        var revision = submission.Revision;

        // Act
        submission.MarkAsReal(enforceSingleSubmissionGate: true);

        // Assert
        submission.RestrictionKey.Should().Be(RestrictionKey);
        submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().BeEmpty();
        submission.Revision.Should().Be(revision);
    }
}

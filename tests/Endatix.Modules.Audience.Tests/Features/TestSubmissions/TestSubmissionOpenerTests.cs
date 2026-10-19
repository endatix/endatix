using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Features.TestSubmissions;

namespace Endatix.Modules.Audience.Tests.Features.TestSubmissions;

public sealed class TestSubmissionOpenerTests
{
    private static readonly TestSubject _subject = new(
        TenantId: 1,
        FormId: 10,
        ActiveDefinitionId: 20,
        MembershipId: 30,
        MemberId: 40,
        Identifier: "ada@example.com");

    [Fact]
    public void Build_IsAStartedTestSubmissionOnTheActiveDefinition()
    {
        var submission = TestSubmissionOpener.Build(_subject, [], DateTime.UnixEpoch);

        submission.IsTestSubmission.Should().BeTrue();
        submission.FormId.Should().Be(10);
        submission.FormDefinitionId.Should().Be(20);
        submission.IsComplete.Should().BeFalse();
        submission.StartedAt.Should().NotBeNull();
        submission.JsonData.Should().Be("{}");
    }

    [Fact]
    public void Build_HasNoSubmitterLinkOrOneResponseKey_AndIsLabelledWithTheIdentifier()
    {
        var submission = TestSubmissionOpener.Build(_subject, [], DateTime.UnixEpoch);

        submission.SubmitterId.Should().BeNull();
        submission.SubmitterDisplayId.Should().Be("ada@example.com");
        submission.AudienceLinkId.Should().BeNull();
        submission.RestrictionKey.Should().BeNull();
    }

    [Fact]
    public void Build_FreezesTheValuesAsATestSnapshotOfTheMember()
    {
        var submission = TestSubmissionOpener.Build(
            _subject,
            [new SnapshotCell(1, "city", "City", AudienceDataTypeCodes.Text, "Columbus")],
            DateTime.UnixEpoch);

        var read = PersonalizationReader.Read(submission.PersonalizationSnapshot);
        read!.Source.Should().Be(PersonalizationSources.Test);
        read.MemberId.Should().Be(40);
        read.Identifier.Should().Be("ada@example.com");
        read.Variables.GetProperty("city").GetString().Should().Be("Columbus");
    }
}

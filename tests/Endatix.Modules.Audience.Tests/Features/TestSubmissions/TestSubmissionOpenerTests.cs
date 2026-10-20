using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Endatix.Modules.Audience.Persistence;

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

    [Fact]
    public async Task OpenAsync_WritesTheTokenInTheInsert()
    {
        var submissions = Substitute.For<IRepository<Submission>>();
        var tokens = Substitute.For<ISubmissionTokenService>();
        var form = Form.Create(new FormCreateArgs(TenantId: _subject.TenantId, Name: "Survey", SubmissionTokenExpiryHours: 24));
        var token = new Token(24);
        tokens.NewTokenAsync(form, Arg.Any<CancellationToken>()).Returns(token);
        var opener = new TestSubmissionOpener(Substitute.For<IAudienceDbContext>(), submissions, tokens);

        var result = await opener.OpenAsync(new TestOpening(_subject, form), [], CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Token.Should().Be(token.Value);
        await submissions.Received(1).AddAsync(
            Arg.Is<Submission>(submission => submission.Token == token),
            Arg.Any<CancellationToken>());
        await tokens.DidNotReceive().ObtainTokenAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenAsync_WhenTheTokenCannotBeMinted_InsertsNothing()
    {
        var submissions = Substitute.For<IRepository<Submission>>();
        var tokens = Substitute.For<ISubmissionTokenService>();
        var form = Form.Create(new FormCreateArgs(TenantId: _subject.TenantId, Name: "Survey"));
        tokens.NewTokenAsync(form, Arg.Any<CancellationToken>())
            .Returns<Task<Token>>(_ => throw new InvalidOperationException("settings unavailable"));
        var opener = new TestSubmissionOpener(Substitute.For<IAudienceDbContext>(), submissions, tokens);

        var act = () => opener.OpenAsync(new TestOpening(_subject, form), [], CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await submissions.DidNotReceive().AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
    }
}

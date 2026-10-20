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
    public void Build_Subject_IsAStartedTestSubmissionOnTheActiveDefinition()
    {
        // Act
        var submission = TestSubmissionOpener.Build(_subject, [], DateTime.UnixEpoch);

        // Assert
        submission.IsTestSubmission.Should().BeTrue();
        submission.FormId.Should().Be(10);
        submission.FormDefinitionId.Should().Be(20);
        submission.IsComplete.Should().BeFalse();
        submission.StartedAt.Should().NotBeNull();
        submission.JsonData.Should().Be("{}");
    }

    [Fact]
    public void Build_Subject_HasNoSubmitterLinkOrOneResponseKey()
    {
        // Act
        var submission = TestSubmissionOpener.Build(_subject, [], DateTime.UnixEpoch);

        // Assert
        submission.SubmitterId.Should().BeNull();
        submission.AudienceLinkId.Should().BeNull();
        submission.RestrictionKey.Should().BeNull();
    }

    [Fact]
    public void Build_Subject_IsLabelledWithTheIdentifier()
    {
        // Act
        var submission = TestSubmissionOpener.Build(_subject, [], DateTime.UnixEpoch);

        // Assert
        submission.SubmitterDisplayId.Should().Be("ada@example.com");
    }

    [Fact]
    public void Build_WithValues_FreezesThemAsATestSnapshotOfTheMember()
    {
        // Arrange
        SnapshotCell[] cells = [new SnapshotCell(1, "city", "City", AudienceDataTypeCodes.Text, "Columbus")];

        // Act
        var submission = TestSubmissionOpener.Build(_subject, cells, DateTime.UnixEpoch);

        // Assert
        var read = PersonalizationReader.Read(submission.PersonalizationSnapshot);
        read!.Source.Should().Be(PersonalizationSources.Test);
        read.MemberId.Should().Be(40);
        read.Identifier.Should().Be("ada@example.com");
        read.Variables.GetProperty("city").GetString().Should().Be("Columbus");
    }

    [Fact]
    public async Task PrepareAsync_TokenMinted_PutsItOnTheSubmissionWithoutSaving()
    {
        // Arrange
        var (opener, submissions, tokens) = Opener();
        var form = Form.Create(new FormCreateArgs(TenantId: _subject.TenantId, Name: "Survey", SubmissionTokenExpiryHours: 24));
        var token = new Token(24);
        tokens.NewTokenAsync(form, Arg.Any<CancellationToken>()).Returns(token);

        // Act
        var prepared = await opener.PrepareAsync(new TestOpening(_subject, form), [], CancellationToken.None);

        // Assert
        prepared.Token.Should().BeSameAs(token);
        await submissions.DidNotReceive().AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
        await tokens.DidNotReceive().ObtainTokenAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_PreparedSubmission_InsertsItAndReturnsItsToken()
    {
        // Arrange
        var (opener, submissions, _) = Opener();
        var prepared = TestSubmissionOpener.Build(_subject, [], DateTime.UnixEpoch);
        var token = new Token(24);
        prepared.UpdateToken(token);

        // Act
        var result = await opener.AddAsync(prepared, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Token.Should().Be(token.Value);
        await submissions.Received(1).AddAsync(prepared, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrepareAsync_TokenCannotBeMinted_Throws()
    {
        // Arrange
        var (opener, _, tokens) = Opener();
        var form = Form.Create(new FormCreateArgs(TenantId: _subject.TenantId, Name: "Survey"));
        tokens.NewTokenAsync(form, Arg.Any<CancellationToken>())
            .Returns<Task<Token>>(_ => throw new InvalidOperationException("settings unavailable"));

        // Act
        var act = () => opener.PrepareAsync(new TestOpening(_subject, form), [], CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static (TestSubmissionOpener Opener, IRepository<Submission> Submissions, ISubmissionTokenService Tokens) Opener()
    {
        var submissions = Substitute.For<IRepository<Submission>>();
        var tokens = Substitute.For<ISubmissionTokenService>();
        return (new TestSubmissionOpener(Substitute.For<IAudienceDbContext>(), submissions, tokens), submissions, tokens);
    }
}

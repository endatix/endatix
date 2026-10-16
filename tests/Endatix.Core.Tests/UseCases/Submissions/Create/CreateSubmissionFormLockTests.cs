using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Tests.TestUtils;
using Endatix.Core.UseCases.Submissions.Create;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Submissions.Create;

/// <summary>The insert runs under the form's lock, after reading the form again.</summary>
public class CreateSubmissionFormLockTests
{
    private const long FormId = 1;

    private readonly IRepository<Submission> _submissions = Substitute.For<IRepository<Submission>>();
    private readonly IFormsRepository _forms = Substitute.For<IFormsRepository>();
    private readonly FormSubmissionsLockStub _formLock = new();
    private readonly ICurrentUserAuthorizationService _authorization = Substitute.For<ICurrentUserAuthorizationService>();
    private readonly ISubmitterResolver _submitters = Substitute.For<ISubmitterResolver>();
    private readonly CreateSubmissionHandler _handler;

    public CreateSubmissionFormLockTests()
    {
        var recaptcha = Substitute.For<IReCaptchaPolicyService>();
        recaptcha.ValidateReCaptchaAsync(Arg.Any<SubmissionVerificationContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _submitters.EnsureSubmitterAsync(Arg.Any<SubmitterResolveContext>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitterResolution(null, null, null));
        StubPublicForm(limitOnePerUser: false);
        _handler = new CreateSubmissionHandler(
            _submissions,
            _forms,
            Substitute.For<ISubmissionTokenService>(),
            recaptcha,
            Substitute.For<IMediator>(),
            _authorization,
            _submitters,
            _formLock.Lock);
    }

    [Fact]
    public async Task Handle_FormStillLiveUnderTheLock_InsertsThenCommits()
    {
        // Arrange
        StubFormLiveUnderLock(true);

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        Received.InOrder(() =>
        {
            _formLock.Lock.BeginAddingAsync(FormId, Arg.Any<CancellationToken>());
            _forms.AnyAsync(Arg.Any<FormSpecifications.ByIdForTenant>(), Arg.Any<CancellationToken>());
            _submissions.AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
            _formLock.Section.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_FormDeletedWhileWaitingForTheLock_ReturnsNotFoundWithoutInserting()
    {
        // Arrange
        StubFormLiveUnderLock(false);

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _submissions.DidNotReceive().AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
        await _formLock.Section.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await _formLock.Section.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Handle_FormLockUnavailable_ReturnsUnavailableWithoutInserting()
    {
        // Arrange
        _formLock.Lock.BeginAddingAsync(FormId, Arg.Any<CancellationToken>())
            .Returns(Result<IFormSubmissionsSection>.Unavailable("busy"));

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unavailable);
        await _submissions.DidNotReceive().AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InsertRefusedAsDuplicate_EndsTheSectionBeforeReadingTheConflict()
    {
        // Arrange
        StubOneResponsePerPerson();
        StubFormLiveUnderLock(true);
        _submissions.AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new DuplicateSubmissionException("Duplicate."));

        // Act
        var result = await _handler.Handle(Command(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        await _formLock.Section.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            _formLock.Section.DisposeAsync();
            _submissions.FirstOrDefaultAsync(
                Arg.Any<SubmissionByFormIdAndSubmitterIdSpec>(),
                Arg.Any<CancellationToken>());
        });
    }

    private void StubFormLiveUnderLock(bool live) =>
        _forms.AnyAsync(Arg.Any<FormSpecifications.ByIdForTenant>(), Arg.Any<CancellationToken>())
            .Returns(live);

    private static CreateSubmissionCommand Command() =>
        new(FormId, "{}", null, null, false, null, Actions.Submissions.Create);

    /// <summary>A known respondent on a one-response form, so a refused insert is a duplicate of theirs.</summary>
    private void StubOneResponsePerPerson()
    {
        StubPublicForm(limitOnePerUser: true);
        _submitters.EnsureSubmitterAsync(Arg.Any<SubmitterResolveContext>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitterResolution(123, "123", null));
        _authorization.HasPermissionAsync(Actions.Forms.Test, Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
    }

    private void StubPublicForm(bool limitOnePerUser)
    {
        var form = Form.Create(new FormCreateArgs(
            TenantId: SampleData.TENANT_ID,
            Name: "Test Form",
            IsEnabled: true,
            IsPublic: true,
            LimitOnePerUser: limitOnePerUser));
        form.Id = FormId;
        var definition = new FormDefinition(SampleData.TENANT_ID) { Id = 2 };
        form.AddFormDefinition(definition);
        form.SetActiveFormDefinition(definition);
        _forms.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(form);
    }
}

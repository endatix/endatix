using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Tests.TestUtils;
using Endatix.Core.UseCases.Submissions.Create;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Submissions.Create;

/// <summary>
/// An on-behalf create as a test submission: an explicit <c>isTest</c> needs <c>forms.test</c>; without one, the
/// Audience test-participant flag decides. A test create holds no one-response slot.
/// </summary>
public sealed class CreateTestSubmissionOnBehalfTests
{
    private const long SUBMITTER_ID = 123;

    private readonly IRepository<Submission> _submissions = Substitute.For<IRepository<Submission>>();
    private readonly IFormsRepository _forms = Substitute.For<IFormsRepository>();
    private readonly ICurrentUserAuthorizationService _authorization = Substitute.For<ICurrentUserAuthorizationService>();
    private readonly ISubmitterResolver _submitterResolver = Substitute.For<ISubmitterResolver>();
    private readonly CreateSubmissionHandler _handler;

    public CreateTestSubmissionOnBehalfTests()
    {
        var form = Form.Create(new FormCreateArgs(
            TenantId: SampleData.TENANT_ID, Name: "Panel", IsEnabled: true, IsPublic: false, LimitOnePerUser: true));
        form.Id = 1;
        var definition = new FormDefinition(SampleData.TENANT_ID) { Id = 2 };
        form.AddFormDefinition(definition);
        form.SetActiveFormDefinition(definition);
        _forms.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(form);
        _forms.FirstOrDefaultAsync(Arg.Any<FormSpecifications.OpenFormActiveDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns((long?)2);
        _authorization.ValidateAccessAsync(Actions.Submissions.CreateOnBehalf, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _submitterResolver.EnsureSubmitterAsync(Arg.Any<SubmitterResolveContext>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitterResolution(SUBMITTER_ID, "P-123", null));
        var recaptcha = Substitute.For<IReCaptchaPolicyService>();
        recaptcha.ValidateReCaptchaAsync(Arg.Any<SubmissionVerificationContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _handler = new CreateSubmissionHandler(
            _submissions,
            _forms,
            Substitute.For<ISubmissionTokenService>(),
            recaptcha,
            Substitute.For<IMediator>(),
            _authorization,
            _submitterResolver,
            new FormSubmissionsLockStub().Lock);
    }

    [Fact]
    public async Task An_explicit_test_with_forms_test_creates_a_test_submission_without_a_slot()
    {
        // Arrange
        AllowTesting(true);
        GivenAnExistingSubmission();

        // Act
        var result = await _handler.Handle(OnBehalf(isTest: true), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.IsTestSubmission.Should().BeTrue();
        result.Value.RestrictionKey.Should().BeNull();
    }

    [Fact]
    public async Task An_explicit_test_without_forms_test_is_forbidden_and_creates_nothing()
    {
        // Arrange
        AllowTesting(false);

        // Act
        var result = await _handler.Handle(OnBehalf(isTest: true), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        await _submissions.DidNotReceive().AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
        await _submitterResolver.DidNotReceive()
            .EnsureSubmitterAsync(Arg.Any<SubmitterResolveContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_test_participant_creates_a_test_submission_without_forms_test()
    {
        // Arrange
        GivenAnExistingSubmission();

        // Act
        var result = await _handler.Handle(OnBehalf(isTestParticipant: true), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.IsTestSubmission.Should().BeTrue();
        result.Value.RestrictionKey.Should().BeNull();
        await _authorization.DidNotReceive().HasPermissionAsync(Actions.Forms.Test, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_explicit_real_create_for_a_test_participant_creates_a_real_submission_with_a_slot()
    {
        // Act
        var result = await _handler.Handle(OnBehalf(isTest: false, isTestParticipant: true), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.IsTestSubmission.Should().BeFalse();
        result.Value.RestrictionKey.Should().Be($"SingleSubmission:Form:1:Submitter:{SUBMITTER_ID}");
        await _authorization.DidNotReceive().HasPermissionAsync(Actions.Forms.Test, Arg.Any<CancellationToken>());
    }

    private static CreateSubmissionCommand OnBehalf(bool? isTest = null, bool isTestParticipant = false) =>
        new(
            FormId: 1,
            JsonData: "{ }",
            Metadata: null,
            CurrentPage: null,
            IsComplete: false,
            ReCaptchaToken: null,
            RequiredPermission: Actions.Submissions.CreateOnBehalf,
            Submitter: new SubmitterInput("panelist-123", "P-123", "Integration"),
            IsTest: isTest,
            IsTestParticipant: isTestParticipant);

    private void AllowTesting(bool allowed) =>
        _authorization.HasPermissionAsync(Actions.Forms.Test, Arg.Any<CancellationToken>())
            .Returns(Result.Success(allowed));

    /// <summary>A real submission already holds the person's slot; a test create must not run into it.</summary>
    private void GivenAnExistingSubmission() =>
        _submissions.AnyAsync(Arg.Any<SubmissionByFormIdAndSubmitterIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);
}

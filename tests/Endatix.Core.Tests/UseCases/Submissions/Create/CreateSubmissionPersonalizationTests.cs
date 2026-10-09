using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions.Create;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Submissions.Create;

public class CreateSubmissionPersonalizationTests
{
    private readonly IRepository<Submission> _submissions = Substitute.For<IRepository<Submission>>();
    private readonly IFormsRepository _forms = Substitute.For<IFormsRepository>();
    private readonly ICurrentUserAuthorizationService _authorization = Substitute.For<ICurrentUserAuthorizationService>();
    private readonly ISubmitterResolver _submitters = Substitute.For<ISubmitterResolver>();
    private readonly CreateSubmissionHandler _handler;

    public CreateSubmissionPersonalizationTests()
    {
        var recaptcha = Substitute.For<IReCaptchaPolicyService>();
        recaptcha.ValidateReCaptchaAsync(Arg.Any<SubmissionVerificationContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _submitters.EnsureSubmitterAsync(Arg.Any<SubmitterResolveContext>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitterResolution(123, "123", null));
        _handler = new CreateSubmissionHandler(
            _submissions,
            _forms,
            Substitute.For<ISubmissionTokenService>(),
            recaptcha,
            Substitute.For<IMediator>(),
            _authorization,
            _submitters);
    }

    [Fact]
    public async Task Handle_OnBehalfWithSnapshot_FreezesPersonalization()
    {
        StubForm(limitOne: false);
        const string snapshot = """{"schemaVersion":1,"source":"on_behalf","variables":{}}""";
        var request = new CreateSubmissionCommand(
            1, "{}", null, null, false, null, Actions.Submissions.CreateOnBehalf,
            PersonalizationSnapshot: snapshot);

        var result = await _handler.Handle(request, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Created);
        result.Value.PersonalizationSnapshot.Should().Be(snapshot);
    }

    [Fact]
    public async Task Handle_Duplicate_ReturnsExistingSubmissionId()
    {
        StubForm(limitOne: true);
        StubExisting(99);

        var result = await _handler.Handle(
            new CreateSubmissionCommand(1, "{}", null, null, true, "token", Actions.Submissions.Create),
            CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.ValidationErrors.Should().ContainSingle(error =>
            error.ErrorCode == "submission_exists" && error.ErrorMessage == "99");
    }

    private void StubForm(bool limitOne)
    {
        var form = Form.Create(new FormCreateArgs(
            TenantId: SampleData.TENANT_ID,
            Name: "Test Form",
            IsEnabled: true,
            IsPublic: false,
            LimitOnePerUser: limitOne));
        form.Id = 1;
        var definition = new FormDefinition(SampleData.TENANT_ID) { Id = 2 };
        form.AddFormDefinition(definition);
        form.SetActiveFormDefinition(definition);
        _forms.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(form);
        _authorization.ValidateAccessAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _authorization.HasPermissionAsync(Actions.Forms.Test, Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
    }

    private void StubExisting(long submissionId)
    {
        var existing = Submission.Create(submissionId, new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID, FormId: 1, FormDefinitionId: 2, JsonData: "{}"));
        _submissions.AnyAsync(Arg.Any<SubmissionByFormIdAndSubmitterIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _submissions.FirstOrDefaultAsync(Arg.Any<SubmissionByFormIdAndSubmitterIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(existing);
    }
}

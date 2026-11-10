using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Tests.Entities.Lifecycle;
using Endatix.Core.Tests.TestUtils;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.Submissions.Create;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Submissions.Create;

/// <summary>A respondent create may end the interview in a closed outcome, but never in one only the server sets.</summary>
public class CreateSubmissionCollectionOutcomeTests
{
    private readonly IRepository<Submission> _submissions = Substitute.For<IRepository<Submission>>();
    private readonly CreateSubmissionHandler _handler;

    public CreateSubmissionCollectionOutcomeTests()
    {
        var forms = Substitute.For<IFormsRepository>();
        forms.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(PublicForm());
        forms.FirstOrDefaultAsync(Arg.Any<FormSpecifications.OpenFormActiveDefinitionId>(), Arg.Any<CancellationToken>())
            .Returns((long?)2);
        var recaptcha = Substitute.For<IReCaptchaPolicyService>();
        recaptcha.ValidateReCaptchaAsync(Arg.Any<SubmissionVerificationContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        var submitters = Substitute.For<ISubmitterResolver>();
        submitters.EnsureSubmitterAsync(Arg.Any<SubmitterResolveContext>(), Arg.Any<CancellationToken>())
            .Returns(new SubmitterResolution(null, null, null));
        _handler = new CreateSubmissionHandler(
            _submissions,
            forms,
            Substitute.For<ISubmissionTokenService>(),
            recaptcha,
            Substitute.For<IMediator>(),
            Substitute.For<ICurrentUserAuthorizationService>(),
            submitters,
            new FormSubmissionsLockStub().Lock);
    }

    [Theory]
    [InlineData(CollectionStatusCodes.QuotaFull)]
    [InlineData(CollectionStatusCodes.Cancelled)]
    public async Task Handle_ServerOnlyOutcome_ReturnsServerOnlyMessageAndAddsNothing(string outcome)
    {
        // Act
        var result = await _handler.Handle(Command(outcome), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(e => e.ErrorMessage == CollectionOutcomes.SERVER_ONLY_MESSAGE);
        await _submissions.DidNotReceive().AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(CollectionStatusCodes.ScreenOut)]
    [InlineData(HostCollectionStatuses.PanelOverquota)]
    public async Task Handle_RespondentClosedOutcome_CreatesTheSubmissionInIt(string outcome)
    {
        // Act
        var result = await _handler.Handle(Command(outcome), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.CollectionStatus.Code.Should().Be(outcome);
    }

    private static CreateSubmissionCommand Command(string outcome) =>
        new(
            FormId: 1,
            JsonData: "{ \"field\": \"value\" }",
            IsComplete: false,
            CurrentPage: 1,
            Metadata: null,
            ReCaptchaToken: "test-token",
            RequiredPermission: "submissions.create",
            CollectionOutcome: outcome);

    private static Form PublicForm()
    {
        var form = Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form", IsEnabled: true));
        form.Id = 1;
        var formDefinition = new FormDefinition(SampleData.TENANT_ID) { Id = 2 };
        form.AddFormDefinition(formDefinition);
        form.SetActiveFormDefinition(formDefinition);
        return form;
    }
}

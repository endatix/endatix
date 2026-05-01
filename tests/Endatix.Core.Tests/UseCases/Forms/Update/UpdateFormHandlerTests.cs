using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Forms.Update;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Forms.Update;

public class UpdateFormHandlerTests
{
    private readonly IRepository<Form> _repository;
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IMediator _mediator;
    private readonly UpdateFormHandler _handler;

    public UpdateFormHandlerTests()
    {
        _repository = Substitute.For<IRepository<Form>>();
        _submissionRepository = Substitute.For<IRepository<Submission>>();
        _mediator = Substitute.For<IMediator>();
        _handler = new UpdateFormHandler(_repository, _submissionRepository, _mediator);
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_1, SampleData.FORM_DESCRIPTION_1, true);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns((Form)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesForm()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1) { Id = 1, Description = SampleData.FORM_DESCRIPTION_1, IsEnabled = true };
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_2, SampleData.FORM_DESCRIPTION_2, false);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(request.Name);
        result.Value.Description.Should().Be(request.Description);
        result.Value.IsEnabled.Should().Be(request.IsEnabled);
        await _repository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_PublishesFormUpdatedEvent()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1) { Id = 1, Description = SampleData.FORM_DESCRIPTION_1, IsEnabled = true };
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_2, SampleData.FORM_DESCRIPTION_2, false);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Publish(Arg.Is<FormUpdatedEvent>(e => e.Form == form), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EnabledStateChanged_PublishesFormEnabledStateChangedEvent()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1) { Id = 1, Description = SampleData.FORM_DESCRIPTION_1, IsEnabled = true };
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_1, SampleData.FORM_DESCRIPTION_1, false);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Publish(Arg.Is<FormEnabledStateChangedEvent>(e => e.Form == form && e.IsEnabled == false), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithWebHookSettingsJson_UpdatesWebHookConfiguration()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1) { Id = 1 };
        var webHookJson = """
        {
            "Events": {
                "SubmissionCompleted": {
                    "IsEnabled": true,
                    "WebHookEndpoints": [
                        {
                            "Url": "https://api.example.com/webhook"
                        }
                    ]
                }
            }
        }
        """;
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_1, SampleData.FORM_DESCRIPTION_1, true, webHookJson);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        form.WebHookSettings.Should().NotBeNull();
        form.WebHookSettings.Events.Should().ContainKey("SubmissionCompleted");
        form.WebHookSettings.Events["SubmissionCompleted"].IsEnabled.Should().BeTrue();
        form.WebHookSettings.Events["SubmissionCompleted"].WebHookEndpoints.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithEmptyWebHookSettingsJson_ClearsWebHookConfiguration()
    {
        // Arrange
        var webHookConfig = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new WebHookEventConfig { IsEnabled = true }
            }
        };
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1) { Id = 1 };
        form.UpdateWebHookSettings(webHookConfig);

        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_1, SampleData.FORM_DESCRIPTION_1, true, "");
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        form.WebHookSettingsJson.Should().BeNull();
        form.WebHookSettings.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNullWebHookSettingsJson_ClearsWebHookConfiguration()
    {
        // Arrange
        var webHookConfig = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new WebHookEventConfig { IsEnabled = true }
            }
        };
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1) { Id = 1 };
        form.UpdateWebHookSettings(webHookConfig);

        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_1, SampleData.FORM_DESCRIPTION_1, true, null);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        form.WebHookSettingsJson.Should().BeNull();
        form.WebHookSettings.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DisablingLimitOnePerUserAfterEnabled_ReturnsConflict()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: false, limitOnePerUser: true) { Id = 1 };
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_1, SampleData.FORM_DESCRIPTION_1, true, null, false);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Errors.Should().Contain("Single submission gate cannot be disabled after it has been enabled.");
    }

    [Fact]
    public async Task Handle_EnablingLimitOnePerUserWithDuplicateEligibleSubmissions_ReturnsConflict()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: false, limitOnePerUser: false) { Id = 1 };
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_1, SampleData.FORM_DESCRIPTION_1, true, null, true);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
            .Returns(form);
        _submissionRepository.ListAsync(
                Arg.Any<EligibleSingleSubmissionGateSubmitterIdsByFormIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(["user-1", "user-1"]);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Errors.Should().Contain("Cannot enable single submission gate because this form already has duplicate submissions.");
    }

    [Fact]
    public async Task Handle_EnablingLimitOnePerUserWithOnlyTestDuplicates_Succeeds()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: false, limitOnePerUser: false) { Id = 1 };
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_1, SampleData.FORM_DESCRIPTION_1, true, null, true);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
            .Returns(form);
        _submissionRepository.ListAsync(
                Arg.Any<EligibleSingleSubmissionGateSubmitterIdsByFormIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.LimitOnePerUser.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EnablingLimitOnePerUserWithWhitespaceSubmittedByValues_Succeeds()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: false, limitOnePerUser: false) { Id = 1 };
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_1, SampleData.FORM_DESCRIPTION_1, true, null, true);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
            .Returns(form);
        _submissionRepository.ListAsync(
                Arg.Any<EligibleSingleSubmissionGateSubmitterIdsByFormIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(["   ", "\t", Environment.NewLine]);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.LimitOnePerUser.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EnablingLimitOnePerUserOnPublicForm_ReturnsConflict()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: true, limitOnePerUser: false) { Id = 1 };
        var request = new UpdateFormCommand(1, SampleData.FORM_NAME_1, SampleData.FORM_DESCRIPTION_1, true, null, true);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Errors.Should().Contain("A single-submission form cannot be made public.");
    }

    [Fact]
    public async Task Handle_OmittedLimitOnePerUser_PreservesExistingEnabledValue()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: false, limitOnePerUser: true) { Id = 1 };
        var request = new UpdateFormCommand(
            formId: 1,
            name: SampleData.FORM_NAME_1,
            description: SampleData.FORM_DESCRIPTION_1,
            isEnabled: true,
            webHookSettingsJson: null,
            limitOnePerUser: null);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.LimitOnePerUser.Should().BeTrue();
    }
}

using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.Update;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Forms.Update;

public class UpdateFormHandlerTests
{
    private readonly IRepository<Form> _repository;
    private readonly IMediator _mediator;
    private readonly UpdateFormHandler _handler;

    public UpdateFormHandlerTests()
    {
        _repository = Substitute.For<IRepository<Form>>();
        _mediator = Substitute.For<IMediator>();
        _handler = new UpdateFormHandler(_repository, _mediator);
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
}

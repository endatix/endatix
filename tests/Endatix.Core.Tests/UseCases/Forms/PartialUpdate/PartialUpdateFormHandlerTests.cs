using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.PartialUpdate;
using MediatR;

namespace Endatix.Core.Tests.UseCases.Forms.PartialUpdate;

public class PartialUpdateFormHandlerTests
{
    private readonly IRepository<Form> _repository;
    private readonly IRepository<Theme> _themeRepository;
    private readonly IMediator _mediator;
    private readonly PartialUpdateFormHandler _handler;

    public PartialUpdateFormHandlerTests()
    {
        _repository = Substitute.For<IRepository<Form>>();
        _themeRepository = Substitute.For<IRepository<Theme>>();
        _mediator = Substitute.For<IMediator>();
        _handler = new PartialUpdateFormHandler(_repository, _themeRepository, _mediator);
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        Form? notFoundForm = null;
        var request = new PartialUpdateFormCommand(1, null, null, null, null);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(notFoundForm);

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
        var form = new Form(SampleData.TENANT_ID, "Test Form")
        {
            Id = 1,
            Name = SampleData.FORM_NAME_1,
            Description = SampleData.FORM_DESCRIPTION_1,
            IsEnabled = true
        };
        var theme = new Theme(SampleData.TENANT_ID, "Test Theme", "{ \"background\": \"#FFFFFF\" }") { Id = 4 };
        form.SetTheme(theme);
        var request = new PartialUpdateFormCommand(1, SampleData.FORM_NAME_2, SampleData.FORM_DESCRIPTION_2, false, null);
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
        result.Value.IsEnabled.Should().Be(request.IsEnabled!.Value);
        result.Value.ThemeId.Should().Be(theme.Id);
        await _repository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PartialUpdate_UpdatesOnlySpecifiedFields()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1, Name = SampleData.FORM_NAME_1, Description = SampleData.FORM_DESCRIPTION_1, IsEnabled = true };
        var oldTheme = new Theme(SampleData.TENANT_ID, "Test Theme", "{ \"background\": \"#FFFFFF\" }") { Id = 3 };
        var newTheme = new Theme(SampleData.TENANT_ID, "Test Theme", "{ \"background\": \"#000000\" }") { Id = 4 };
        var request = new PartialUpdateFormCommand(
           formId: 1, 
           name: null,
           description: SampleData.FORM_DESCRIPTION_2,
           isEnabled: null,
           themeId: 4
        );
        form.SetTheme(oldTheme);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);
        _themeRepository.GetByIdAsync(request.ThemeId!.Value, Arg.Any<CancellationToken>())
                    .Returns(newTheme);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(form.Name);
        result.Value.Description.Should().Be(request.Description);
        result.Value.IsEnabled.Should().Be(form.IsEnabled);
        result.Value.ThemeId.Should().Be(newTheme.Id);
        await _repository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_PublishesFormUpdatedEvent()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form")
        {
            Id = 1,
            Name = SampleData.FORM_NAME_1,
            Description = SampleData.FORM_DESCRIPTION_1,
            IsEnabled = true
        };
        var request = new PartialUpdateFormCommand(1, SampleData.FORM_NAME_2, SampleData.FORM_DESCRIPTION_2, false, null);
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
        var form = new Form(SampleData.TENANT_ID, "Test Form")
        {
            Id = 1,
            Name = SampleData.FORM_NAME_1,
            Description = SampleData.FORM_DESCRIPTION_1,
            IsEnabled = true
        };
        var request = new PartialUpdateFormCommand(1, null, null, false, null);
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
        var request = new PartialUpdateFormCommand(1, null, null, null, null, webHookJson);
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

        var request = new PartialUpdateFormCommand(1, null, null, null, null, "");
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
    public async Task Handle_PartialUpdateWithOtherFieldsAndWebHookSettings_UpdatesBothCorrectly()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1)
        {
            Id = 1,
            Description = SampleData.FORM_DESCRIPTION_1,
            IsEnabled = true
        };
        var webHookJson = """
        {
            "Events": {
                "FormUpdated": {
                    "IsEnabled": true,
                    "WebHookEndpoints": [
                        {
                            "Url": "https://api.example.com/form-updated"
                        }
                    ]
                }
            }
        }
        """;
        var request = new PartialUpdateFormCommand(1, SampleData.FORM_NAME_2, null, false, null, webHookJson);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        form.Name.Should().Be(SampleData.FORM_NAME_2);
        form.Description.Should().Be(SampleData.FORM_DESCRIPTION_1); // Not updated
        form.IsEnabled.Should().BeFalse();
        form.WebHookSettings.Events.Should().ContainKey("FormUpdated");
        form.WebHookSettings.Events["FormUpdated"].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithNullWebHookSettingsJson_PreservesExistingWebHookSettings()
    {
        // Arrange
        var webHookConfig = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new WebHookEventConfig
                {
                    IsEnabled = true,
                    WebHookEndpoints = new List<WebHookEndpointConfig>
                    {
                        new WebHookEndpointConfig { Url = "https://api.example.com/webhook" }
                    }
                }
            }
        };
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1) { Id = 1 };
        form.UpdateWebHookSettings(webHookConfig);

        // Create request with only Name updated, WebHookSettingsJson is null
        var request = new PartialUpdateFormCommand(1, SampleData.FORM_NAME_2, null, null, null, null);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        form.Name.Should().Be(SampleData.FORM_NAME_2); // Name was updated
        form.WebHookSettingsJson.Should().NotBeNull(); // Webhook settings preserved
        form.WebHookSettings.Events.Should().ContainKey("SubmissionCompleted"); // Original webhook still exists
        form.WebHookSettings.Events["SubmissionCompleted"].IsEnabled.Should().BeTrue();
        form.WebHookSettings.Events["SubmissionCompleted"].WebHookEndpoints.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ }")]
    [InlineData("{  }")]
    [InlineData("{\n}")]
    [InlineData("{ \n }")]
    public async Task Handle_WithEmptyJsonObject_ClearsWebHookConfiguration(string emptyJson)
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

        var request = new PartialUpdateFormCommand(1, null, null, null, null, emptyJson);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        form.WebHookSettingsJson.Should().BeNull(); // Cleared by empty JSON object
        form.WebHookSettings.Events.Should().BeEmpty();
    }
}

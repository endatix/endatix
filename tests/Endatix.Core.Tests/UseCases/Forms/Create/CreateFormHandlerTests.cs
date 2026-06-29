using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Folders;
using Endatix.Core.UseCases.Forms.Create;
using TenantSettingsEntity = Endatix.Core.Entities.TenantSettings;

namespace Endatix.Core.Tests.UseCases.Forms.Create;

public class CreateFormHandlerTests
{
    private readonly IFormsRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly CreateFormHandler _handler;

    public CreateFormHandlerTests()
    {
        _repository = Substitute.For<IFormsRepository>();
        _tenantContext = Substitute.For<ITenantContext>();
        _handler = new CreateFormHandler(_repository, _tenantContext, FolderAssignmentPolicyStub.Relaxed(_tenantContext));
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesForm()
    {
        // Arrange
        var request = new CreateFormCommand("Form Name", "Description", true, SampleData.FORM_DEFINITION_JSON_DATA_1);

        var createdForm = new Form(SampleData.TENANT_ID, "Form Name", request.Description, request.IsEnabled)
        {
            Id = 123
        };
        var createdFormDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1)
        {
            Id = 456
        };
        createdForm.AddFormDefinition(createdFormDefinition);

        _repository.CreateFormWithDefinitionAsync(Arg.Do<Form>(form => createdForm = form), Arg.Do<FormDefinition>(fd => createdFormDefinition = fd), Arg.Any<CancellationToken>())
                   .Returns(createdForm);
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(request.Name);
        result.Value.Id.Should().Be(123);
        result.Value.TenantId.Should().Be(SampleData.TENANT_ID);
        result.Value.Description.Should().Be(request.Description);
        result.Value.IsEnabled.Should().Be(request.IsEnabled);

        await _repository.Received(1).CreateFormWithDefinitionAsync(createdForm, createdFormDefinition, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithWebHookSettings_CreatesFormWithWebHookConfiguration()
    {
        // Arrange
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
        var request = new CreateFormCommand("Form Name", "Description", true, SampleData.FORM_DEFINITION_JSON_DATA_1, webHookJson);

        Form? capturedForm = null;
        _repository.CreateFormWithDefinitionAsync(
            Arg.Do<Form>(f => capturedForm = f),
            Arg.Any<FormDefinition>(),
            Arg.Any<CancellationToken>())
                   .Returns(callInfo => callInfo.Arg<Form>());
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        capturedForm.Should().NotBeNull();
        capturedForm!.WebHookSettingsJson.Should().Be(webHookJson);
        capturedForm.WebHookSettings.Should().NotBeNull();
        capturedForm.WebHookSettings.Events.Should().ContainKey("SubmissionCompleted");
    }

    [Fact]
    public async Task Handle_WithoutWebHookSettings_CreatesFormWithNullWebHookConfiguration()
    {
        // Arrange
        var request = new CreateFormCommand("Form Name", "Description", true, SampleData.FORM_DEFINITION_JSON_DATA_1);

        Form? capturedForm = null;
        _repository.CreateFormWithDefinitionAsync(
            Arg.Do<Form>(f => capturedForm = f),
            Arg.Any<FormDefinition>(),
            Arg.Any<CancellationToken>())
                   .Returns(callInfo => callInfo.Arg<Form>());
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        capturedForm.Should().NotBeNull();
        capturedForm!.WebHookSettingsJson.Should().BeNull();
        capturedForm.WebHookSettings.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RequireFolderAssignmentAndMissingFolder_ReturnsError()
    {
        // Arrange
        var request = new CreateFormCommand(
            "Form Name",
            "Description",
            true,
            SampleData.FORM_DEFINITION_JSON_DATA_1);
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);

        var tenantSettingsRepo = Substitute.For<IRepository<TenantSettingsEntity>>();
        var folderRepo = Substitute.For<IRepository<Folder>>();
        var settings = new TenantSettingsEntity(SampleData.TENANT_ID);
        settings.UpdateRequireFolderAssignment(true);
        tenantSettingsRepo
            .FirstOrDefaultAsync(Arg.Any<Ardalis.Specification.ISpecification<TenantSettingsEntity>>(), Arg.Any<CancellationToken>())
            .Returns(settings);
        var strictHelper = new FolderAssignmentPolicy(tenantSettingsRepo, folderRepo, _tenantContext);
        var handler = new CreateFormHandler(_repository, _tenantContext, strictHelper);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain("You must assign a folder.");
        await _repository.DidNotReceive().CreateFormWithDefinitionAsync(
            Arg.Any<Form>(),
            Arg.Any<FormDefinition>(),
            Arg.Any<CancellationToken>());
    }
}

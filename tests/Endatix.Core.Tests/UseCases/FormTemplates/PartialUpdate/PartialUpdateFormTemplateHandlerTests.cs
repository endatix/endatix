using Endatix.Core.Entities;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormTemplates.PartialUpdate;
using Endatix.Core.UseCases.Folders;
using TenantSettingsEntity = Endatix.Core.Entities.TenantSettings;

namespace Endatix.Core.Tests.UseCases.FormTemplates.PartialUpdate;

public class PartialUpdateFormTemplateHandlerTests
{
    private readonly IRepository<FormTemplate> _repository;
    private readonly PartialUpdateFormTemplateHandler _handler;

    public PartialUpdateFormTemplateHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormTemplate>>();
        _handler = new PartialUpdateFormTemplateHandler(_repository, FolderAssignmentPolicyStub.Relaxed(SampleData.TENANT_ID));
    }

    [Fact]
    public async Task Handle_FormTemplateNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        FormTemplate? notFoundTemplate = null;
        var request = new PartialUpdateFormTemplateCommand(1, null, null, null);
        _repository.GetByIdAsync(request.FormTemplateId, Arg.Any<CancellationToken>())
                   .Returns(notFoundTemplate);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form template not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesFormTemplate()
    {
        // Arrange
        var formTemplate = new FormTemplate(SampleData.TENANT_ID, SampleData.FORM_NAME_1)
        {
            Id = 1,
            Description = SampleData.FORM_DESCRIPTION_1,
            JsonData = SampleData.FORM_DEFINITION_JSON_DATA_1
        };

        var request = new PartialUpdateFormTemplateCommand(
            1,
            SampleData.FORM_NAME_2,
            SampleData.FORM_DESCRIPTION_2,
            SampleData.FORM_DEFINITION_JSON_DATA_2
        );

        _repository.GetByIdAsync(request.FormTemplateId, Arg.Any<CancellationToken>())
                   .Returns(formTemplate);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(request.Name);
        result.Value.Description.Should().Be(request.Description);
        result.Value.JsonData.Should().Be(request.JsonData);

        await _repository.Received(1).UpdateAsync(
            Arg.Is<FormTemplate>(ft =>
                ft.Id == formTemplate.Id &&
                ft.Name == request.Name &&
                ft.Description == request.Description &&
                ft.JsonData == request.JsonData
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_PartialUpdate_OnlyUpdatesProvidedProperties()
    {
        // Arrange
        var formTemplate = new FormTemplate(SampleData.TENANT_ID, SampleData.FORM_NAME_1)
        {
            Id = 1,
            Description = SampleData.FORM_DESCRIPTION_1,
            JsonData = SampleData.FORM_DEFINITION_JSON_DATA_1
        };

        var request = new PartialUpdateFormTemplateCommand(1, null, SampleData.FORM_DESCRIPTION_2, null);

        _repository.GetByIdAsync(request.FormTemplateId, Arg.Any<CancellationToken>())
                   .Returns(formTemplate);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(formTemplate.Name);
        result.Value.Description.Should().Be(request.Description);
        result.Value.JsonData.Should().Be(formTemplate.JsonData);
    }

    [Fact]
    public async Task Handle_MovingFromImmutableFolder_ReturnsConflict()
    {
        // Arrange
        var tenantSettingsRepo = Substitute.For<IRepository<TenantSettingsEntity>>();
        var folderRepo = Substitute.For<IRepository<Folder>>();
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        var helper = new FolderAssignmentPolicy(tenantSettingsRepo, folderRepo, tenantContext);

        var immutableFolder = new Folder(SampleData.TENANT_ID, "Immutable", "immutable", "IMMUTABLE")
        {
            Id = 7,
            Immutable = true,
        };
        folderRepo
            .FirstOrDefaultAsync(Arg.Any<Ardalis.Specification.ISpecification<Folder>>(), Arg.Any<CancellationToken>())
            .Returns(immutableFolder);
        tenantSettingsRepo
            .FirstOrDefaultAsync(Arg.Any<Ardalis.Specification.ISpecification<TenantSettingsEntity>>(), Arg.Any<CancellationToken>())
            .Returns((TenantSettingsEntity?)null);

        var handler = new PartialUpdateFormTemplateHandler(_repository, helper);
        var formTemplate = new FormTemplate(SampleData.TENANT_ID, SampleData.FORM_NAME_1, folderId: 7) { Id = 1 };
        var request = new PartialUpdateFormTemplateCommand(1, null, null, null) { FolderId = 9 };
        _repository.GetByIdAsync(request.FormTemplateId, Arg.Any<CancellationToken>())
            .Returns(formTemplate);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Errors.Should().ContainSingle(e => e.Contains("locked folders", StringComparison.OrdinalIgnoreCase));
    }
}
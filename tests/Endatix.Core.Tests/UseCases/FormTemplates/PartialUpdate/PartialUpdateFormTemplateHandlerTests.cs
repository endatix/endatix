using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormTemplates.PartialUpdate;

namespace Endatix.Core.Tests.UseCases.FormTemplates.PartialUpdate;

public class PartialUpdateFormTemplateHandlerTests
{
    private readonly IRepository<FormTemplate> _repository;
    private readonly PartialUpdateFormTemplateHandler _handler;

    public PartialUpdateFormTemplateHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormTemplate>>();
        _handler = new PartialUpdateFormTemplateHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormTemplateNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        FormTemplate? notFoundTemplate = null;
        var request = new PartialUpdateFormTemplateCommand(1, null, null, null, null);
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
            JsonData = SampleData.FORM_DEFINITION_JSON_DATA_1,
            IsEnabled = false
        };

        var request = new PartialUpdateFormTemplateCommand(
            1,
            SampleData.FORM_NAME_2,
            SampleData.FORM_DESCRIPTION_2,
            SampleData.FORM_DEFINITION_JSON_DATA_2,
            true
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
        result.Value.IsEnabled.Should().Be(request.IsEnabled!.Value);

        await _repository.Received(1).UpdateAsync(
            Arg.Is<FormTemplate>(ft =>
                ft.Id == formTemplate.Id &&
                ft.Name == request.Name &&
                ft.Description == request.Description &&
                ft.JsonData == request.JsonData &&
                ft.IsEnabled == request.IsEnabled!.Value
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
            JsonData = SampleData.FORM_DEFINITION_JSON_DATA_1,
            IsEnabled = false
        };

        var request = new PartialUpdateFormTemplateCommand(1, null, SampleData.FORM_DESCRIPTION_2, null, null);

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
        result.Value.IsEnabled.Should().Be(formTemplate.IsEnabled);
    }
} 
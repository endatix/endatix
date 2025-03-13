using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormTemplates.Delete;

namespace Endatix.Core.Tests.UseCases.FormTemplates.Delete;

public class DeleteFormTemplateHandlerTests
{
    private readonly IRepository<FormTemplate> _repository;
    private readonly DeleteFormTemplateHandler _handler;

    public DeleteFormTemplateHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormTemplate>>();
        _handler = new DeleteFormTemplateHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormTemplateNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new DeleteFormTemplateCommand(1);
        _repository.GetByIdAsync(request.FormTemplateId, Arg.Any<CancellationToken>())
                   .Returns((FormTemplate?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form template not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_DeletesFormTemplate()
    {
        // Arrange
        var formTemplate = new FormTemplate(SampleData.TENANT_ID, SampleData.FORM_NAME_1)
        {
            Id = 1,
            Description = SampleData.FORM_DESCRIPTION_1,
            JsonData = SampleData.FORM_DEFINITION_JSON_DATA_1,
            IsEnabled = true
        };
        var request = new DeleteFormTemplateCommand(1);
        
        _repository.GetByIdAsync(request.FormTemplateId, Arg.Any<CancellationToken>())
                   .Returns(formTemplate);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(formTemplate);
        result.Value.IsDeleted.Should().BeTrue();

        await _repository.Received(1).UpdateAsync(
            Arg.Is<FormTemplate>(ft => 
                ft.Id == formTemplate.Id && 
                ft.IsDeleted
            ),
            Arg.Any<CancellationToken>()
        );
    }
} 
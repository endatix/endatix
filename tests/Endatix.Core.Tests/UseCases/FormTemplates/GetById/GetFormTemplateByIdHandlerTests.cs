using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormTemplates.GetById;

namespace Endatix.Core.Tests.UseCases.FormTemplates.GetById;

public class GetFormTemplateByIdHandlerTests
{
    private readonly IRepository<FormTemplate> _repository;
    private readonly GetFormTemplateByIdHandler _handler;

    public GetFormTemplateByIdHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormTemplate>>();
        _handler = new GetFormTemplateByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormTemplateNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        FormTemplate? notFoundTemplate = null;
        var request = new GetFormTemplateByIdQuery(1);
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
    public async Task Handle_ValidRequest_ReturnsFormTemplate()
    {
        // Arrange
        var formTemplate = new FormTemplate(SampleData.TENANT_ID, SampleData.FORM_NAME_1) { Id = 1 };
        var request = new GetFormTemplateByIdQuery(1);
        _repository.GetByIdAsync(request.FormTemplateId, Arg.Any<CancellationToken>())
                   .Returns(formTemplate);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(formTemplate);
    }
} 
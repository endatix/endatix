using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormTemplates;
using Endatix.Core.UseCases.FormTemplates.List;

namespace Endatix.Core.Tests.UseCases.FormTemplates.List;

public class ListFormTemplatesHandlerTests
{
    private readonly IRepository<FormTemplate> _repository;
    private readonly ListFormTemplatesHandler _handler;

    public ListFormTemplatesHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormTemplate>>();
        _handler = new ListFormTemplatesHandler(_repository);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsFormTemplates()
    {
        // Arrange
        var formTemplates = new List<FormTemplateDto>
        {
            new()
            {
                Id = "1",
                Name = SampleData.FORM_NAME_1,
                Description = SampleData.FORM_DESCRIPTION_1,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            },
            new()
            {
                Id = "2",
                Name = SampleData.FORM_NAME_2,
                Description = SampleData.FORM_DESCRIPTION_2,
                IsEnabled = false,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = null
            }
        };

        var request = new ListFormTemplatesQuery(1, 10);
        _repository.ListAsync(
            Arg.Any<FormTemplatesSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(formTemplates);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEquivalentTo(formTemplates);
        
        await _repository.Received(1).ListAsync(
            Arg.Any<FormTemplatesSpec>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsEmptyList()
    {
        // Arrange
        var formTemplates = new List<FormTemplateDto>();
        var request = new ListFormTemplatesQuery(1, 10);
        _repository.ListAsync(
            Arg.Any<FormTemplatesSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(formTemplates);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }
} 
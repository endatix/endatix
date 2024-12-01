using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDefinitions.Create;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.Create;

public class CreateFormDefinitionHandlerTests
{
    private readonly IRepository<FormDefinition> _definitionsRepository;
    private readonly IRepository<Form> _formsRepository;
    private readonly CreateFormDefinitionHandler _handler;

    public CreateFormDefinitionHandlerTests()
    {
        _definitionsRepository = Substitute.For<IRepository<FormDefinition>>();
        _formsRepository = Substitute.For<IRepository<Form>>();
        _handler = new CreateFormDefinitionHandler(_definitionsRepository, _formsRepository);
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new CreateFormDefinitionCommand(1, true, SampleData.FORM_DEFINITION_JSON_DATA_1, true);
        _formsRepository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>()).Returns((Form)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesNewFormDefinition()
    {
        // Arrange
        var request = new CreateFormDefinitionCommand(1, true, SampleData.FORM_DEFINITION_JSON_DATA_1, true);
        var form = new Form(SampleData.FORM_NAME_1){
            Id = request.FormId
        };
        _formsRepository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>()).Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        result.Value.FormId.Should().Be(request.FormId);
        result.Value.IsDraft.Should().Be(request.IsDraft);
        result.Value.JsonData.Should().Be(request.JsonData);
        result.Value.IsActive.Should().Be(request.IsActive);
        await _definitionsRepository.Received(1).AddAsync(Arg.Any<FormDefinition>(), Arg.Any<CancellationToken>());
    }
}

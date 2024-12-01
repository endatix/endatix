using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.Create;

namespace Endatix.Core.Tests.UseCases.Forms.Create;

public class CreateFormHandlerTests
{
    private readonly IFormsRepository _repository;
    private readonly CreateFormHandler _handler;

    public CreateFormHandlerTests()
    {
        _repository = Substitute.For<IFormsRepository>();
        _handler = new CreateFormHandler(_repository);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesForm()
    {
        // Arrange
        var request = new CreateFormCommand("Form Name", "Description", true, SampleData.FORM_DEFINITION_JSON_DATA_1);
        var createdForm = new Form("Form Name", request.Description, request.IsEnabled)
        {
            Id = 123
        };
        var createdFormDefinition = new FormDefinition(createdForm){
            Id = 456
        };
        createdForm.AddFormDefinition(createdFormDefinition);

        _repository.CreateFormWithDefinitionAsync(Arg.Do<Form>(form => createdForm = form), Arg.Do<FormDefinition>(fd => createdFormDefinition = fd), Arg.Any<CancellationToken>())
                   .Returns(createdForm);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(request.Name);
        result.Value.Id.Should().Be(123);
        result.Value.Description.Should().Be(request.Description);
        result.Value.IsEnabled.Should().Be(request.IsEnabled);

        await _repository.Received(1).CreateFormWithDefinitionAsync(createdForm, createdFormDefinition, Arg.Any<CancellationToken>());
    }
}

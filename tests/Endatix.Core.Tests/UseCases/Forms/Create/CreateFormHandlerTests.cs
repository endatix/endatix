using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.Create;
using FluentAssertions;
using NSubstitute;

namespace Endatix.Core.Tests.UseCases.Forms.Create;

public class CreateFormHandlerTests
{
    private readonly IRepository<Form> _repository;
    private readonly CreateFormHandler _handler;

    public CreateFormHandlerTests()
    {
        _repository = Substitute.For<IRepository<Form>>();
        _handler = new CreateFormHandler(_repository);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesForm()
    {
        // Arrange
        var request = new CreateFormCommand("Form Name", "Description", true, SampleData.FORM_DEFINITION_JSON_DATA_1);
        Form? createdForm = null;
        _repository.AddAsync(Arg.Do<Form>(form => createdForm = form), Arg.Any<CancellationToken>())
                   .Returns(createdForm!);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(createdForm);
        
        createdForm.Should().NotBeNull();
        createdForm!.Name.Should().Be(request.Name);
        createdForm.Description.Should().Be(request.Description);
        createdForm.IsEnabled.Should().Be(request.IsEnabled);
        
        await _repository.Received(1).AddAsync(createdForm, Arg.Any<CancellationToken>());
    }
}

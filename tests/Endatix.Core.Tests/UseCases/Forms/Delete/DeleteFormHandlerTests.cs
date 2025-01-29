using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Forms.Delete;

namespace Endatix.Core.Tests.UseCases.Forms.Delete;

public class DeleteFormHandlerTests
{
    private readonly IRepository<Form> _repository;
    private readonly DeleteFormHandler _handler;

    public DeleteFormHandlerTests()
    {
        _repository = Substitute.For<IRepository<Form>>();
        _handler = new DeleteFormHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new DeleteFormCommand(1);
        _repository.SingleOrDefaultAsync(
            Arg.Any<FormWithDefinitionsAndSubmissionsSpec>(),
            Arg.Any<CancellationToken>())
            .Returns((Form?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_DeletesForm()
    {
        // Arrange
        var form = new Form("Test Form") { Id = 1 };
        var request = new DeleteFormCommand(1);
        
        _repository.SingleOrDefaultAsync(
            Arg.Any<FormWithDefinitionsAndSubmissionsSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(form);
        
        await _repository.Received(1).UpdateAsync(
            Arg.Is<Form>(f => f.Id == form.Id),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsDeleteOnForm()
    {
        // Arrange
        var form = new Form("Test Form") { Id = 1 };
        var request = new DeleteFormCommand(1);
        
        _repository.SingleOrDefaultAsync(
            Arg.Any<FormWithDefinitionsAndSubmissionsSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(form);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        form.IsDeleted.Should().BeTrue();
    }
} 
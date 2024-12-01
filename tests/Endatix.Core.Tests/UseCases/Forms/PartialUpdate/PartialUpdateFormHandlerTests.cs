using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.PartialUpdate;

namespace Endatix.Core.Tests.UseCases.Forms.PartialUpdate;

public class PartialUpdateFormHandlerTests
{
    private readonly IRepository<Form> _repository;
    private readonly PartialUpdateFormHandler _handler;

    public PartialUpdateFormHandlerTests()
    {
        _repository = Substitute.For<IRepository<Form>>();
        _handler = new PartialUpdateFormHandler(_repository);
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        Form? notFoundForm = null;
        var request = new PartialUpdateFormCommand(1, null, null, null);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(notFoundForm);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesForm()
    {
        // Arrange
        var form = new Form("Test Form")
        {
            Id = 1,
            Name = SampleData.FORM_NAME_1,
            Description = SampleData.FORM_DESCRIPTION_1,
            IsEnabled = true
        };
        var request = new PartialUpdateFormCommand(1, SampleData.FORM_NAME_2, SampleData.FORM_DESCRIPTION_2, false);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(request.Name);
        result.Value.Description.Should().Be(request.Description);
        result.Value.IsEnabled.Should().Be(request.IsEnabled!.Value);
        await _repository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PartialUpdate_UpdatesOnlySpecifiedFields()
    {
        // Arrange
        var form = new Form("Test Form") { Id = 1, Name = SampleData.FORM_NAME_1, Description = SampleData.FORM_DESCRIPTION_1, IsEnabled = true };
        var request = new PartialUpdateFormCommand(1, null, SampleData.FORM_DESCRIPTION_2, null);
        _repository.GetByIdAsync(request.FormId, Arg.Any<CancellationToken>())
                   .Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(form.Name);
        result.Value.Description.Should().Be(request.Description);
        result.Value.IsEnabled.Should().Be(form.IsEnabled);
        await _repository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }
}

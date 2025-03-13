using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormTemplates;
using Endatix.Core.UseCases.FormTemplates.Delete;

namespace Endatix.Api.Tests.Endpoints.FormTemplates;

public class DeleteTests
{
    private readonly IMediator _mediator;
    private readonly Delete _endpoint;

    public DeleteTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Delete>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formTemplateId = 1L;
        var request = new DeleteFormTemplateRequest { FormTemplateId = formTemplateId };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<DeleteFormTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_FormTemplateNotFound_ReturnsNotFound()
    {
        // Arrange
        var formTemplateId = 1L;
        var request = new DeleteFormTemplateRequest { FormTemplateId = formTemplateId };
        var result = Result.NotFound("Form template not found.");

        _mediator.Send(Arg.Any<DeleteFormTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithDeletedFormTemplateId()
    {
        // Arrange
        var formTemplateId = 1L;
        var request = new DeleteFormTemplateRequest { FormTemplateId = formTemplateId };
        var formTemplate = new FormTemplate(SampleData.TENANT_ID, "Test Template") { Id = formTemplateId };
        var result = Result.Success(formTemplate);

        _mediator.Send(Arg.Any<DeleteFormTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<string>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().Be(formTemplateId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new DeleteFormTemplateRequest { FormTemplateId = 123 };
        var result = Result.Success(new FormTemplate(SampleData.TENANT_ID, "Test Template"));
        
        _mediator.Send(Arg.Any<DeleteFormTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<DeleteFormTemplateCommand>(cmd =>
                cmd.FormTemplateId == request.FormTemplateId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

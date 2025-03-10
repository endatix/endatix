using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Forms;
using Endatix.Core.UseCases.Forms.Delete;

namespace Endatix.Api.Tests.Endpoints.Forms;

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
        var formId = 1L;
        var request = new DeleteFormRequest { FormId = formId };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<DeleteFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_FormNotFound_ReturnsNotFound()
    {
        // Arrange
        var formId = 1L;
        var request = new DeleteFormRequest { FormId = formId };
        var result = Result.NotFound("Form not found");

        _mediator.Send(Arg.Any<DeleteFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithDeletedFormId()
    {
        // Arrange
        var formId = 1L;
        var request = new DeleteFormRequest { FormId = formId };
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = formId };
        var result = Result.Success(form);

        _mediator.Send(Arg.Any<DeleteFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<string>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().Be(formId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new DeleteFormRequest { FormId = 123 };
        var result = Result.Success(new Form(SampleData.TENANT_ID, "Test Form"));
        
        _mediator.Send(Arg.Any<DeleteFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<DeleteFormCommand>(cmd =>
                cmd.FormId == request.FormId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
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
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var formId = 1L;
        var request = new DeleteFormRequest { FormId = formId };
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<DeleteFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_FormNotFound_ReturnsProblemDetails()
    {
        // Arrange
        var formId = 1L;
        var request = new DeleteFormRequest { FormId = formId };
        var result = Result.NotFound("Form not found");

        _mediator.Send(Arg.Any<DeleteFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
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
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

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

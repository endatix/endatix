using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Forms;
using Endatix.Core.UseCases.Forms.GetById;

namespace Endatix.Api.Tests.Endpoints.Forms;

public class GetByIdTests
{
    private readonly IMediator _mediator;
    private readonly GetById _endpoint;

    public GetByIdTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetById>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var request = new GetFormByIdRequest { FormId = formId };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<GetFormByIdQuery>(), Arg.Any<CancellationToken>())
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
        var request = new GetFormByIdRequest { FormId = formId };
        var result = Result.NotFound("Form not found");

        _mediator.Send(Arg.Any<GetFormByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithForm()
    {
        // Arrange
        var formId = 1L;
        var request = new GetFormByIdRequest { FormId = formId };
        var form = new Form("Test Form") { Id = formId };
        var result = Result.Success(form);

        _mediator.Send(Arg.Any<GetFormByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<FormModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(formId.ToString());
        okResult!.Value!.Name.Should().Be(form.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new GetFormByIdRequest { FormId = 123 };
        var result = Result.Success(new Form("Test Form"));
        
        _mediator.Send(Arg.Any<GetFormByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetFormByIdQuery>(query =>
                query.FormId == request.FormId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

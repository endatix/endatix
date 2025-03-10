using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Forms;
using Endatix.Core.UseCases.Forms.Update;

namespace Endatix.Api.Tests.Endpoints.Forms;

public class UpdateTests
{
    private readonly IMediator _mediator;
    private readonly Update _endpoint;

    public UpdateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Update>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var request = new UpdateFormRequest
        {
            FormId = formId,
            Name = "Updated Form",
            Description = "Updated Description",
            IsEnabled = true
        };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<UpdateFormCommand>(), Arg.Any<CancellationToken>())
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
        var request = new UpdateFormRequest
        {
            FormId = formId,
            Name = "Updated Form",
            Description = "Updated Description",
            IsEnabled = true
        };
        var result = Result.NotFound("Form not found");

        _mediator.Send(Arg.Any<UpdateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithUpdatedForm()
    {
        // Arrange
        var formId = 1L;
        var request = new UpdateFormRequest 
        { 
            FormId = formId,
            Name = "Updated Form",
            Description = "Updated Description",
            IsEnabled = true
        };
        
        var form = new Form(SampleData.TENANT_ID, request.Name) { Id = formId };
        var result = Result.Success(form);

        _mediator.Send(Arg.Any<UpdateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<UpdateFormResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(formId.ToString());
        okResult!.Value!.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new UpdateFormRequest
        {
            FormId = 123,
            Name = "Updated Form",
            Description = "Updated Description",
            IsEnabled = true
        };
        var result = Result.Success(new Form(SampleData.TENANT_ID, "Updated Form"));
        
        _mediator.Send(Arg.Any<UpdateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<UpdateFormCommand>(cmd =>
                cmd.FormId == request.FormId &&
                cmd.Name == request.Name &&
                cmd.Description == request.Description &&
                cmd.IsEnabled == request.IsEnabled
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

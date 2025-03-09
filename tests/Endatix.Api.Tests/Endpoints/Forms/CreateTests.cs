using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Forms;
using Endatix.Core.UseCases.Forms.Create;

namespace Endatix.Api.Tests.Endpoints.Forms;

public class CreateTests
{
    private readonly IMediator _mediator;
    private readonly Create _endpoint;

    public CreateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Create>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateFormRequest
        {
            Name = "Test Form",
            IsEnabled = true,
            FormDefinitionJsonData = """{ "type": "object" }"""
        };
        
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsCreatedWithForm()
    {
        // Arrange
        var request = new CreateFormRequest 
        { 
            Name = "Test Form",
            Description = "Test Description",
            IsEnabled = true,
            FormDefinitionJsonData = """{ "type": "object" }"""
        };
        
        var form = new Form(SampleData.TENANT_ID, request.Name) { Id = 1 };
        var result = Result<Form>.Created(form);

        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var createdResult = response.Result as Created<CreateFormResponse>;
        createdResult.Should().NotBeNull();
        createdResult!.Value.Should().NotBeNull();
        createdResult!.Value!.Id.Should().Be(form.Id.ToString());
        createdResult!.Value!.Name.Should().Be(form.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new CreateFormRequest
        {
            Name = "Test Form",
            Description = "Test Description",
            IsEnabled = true,
            FormDefinitionJsonData = """{ "type": "object" }"""
        };
        var result = Result<Form>.Created(new Form(SampleData.TENANT_ID, "Test Form"));
        
        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateFormCommand>(cmd =>
                cmd.Name == request.Name &&
                cmd.Description == request.Description &&
                cmd.IsEnabled == request.IsEnabled &&
                cmd.FormDefinitionJsonData == request.FormDefinitionJsonData
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

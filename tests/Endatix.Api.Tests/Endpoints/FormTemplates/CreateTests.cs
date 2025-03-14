using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormTemplates;
using Endatix.Core.UseCases.FormTemplates.Create;
using Endatix.Api.Tests.TestUtils;

namespace Endatix.Api.Tests.Endpoints.FormTemplates;

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
        var request = new CreateFormTemplateRequest
        {
            Name = "Test Template",
            Description = "Test Description",
            IsEnabled = true,
            JsonData = "{ }"
        };
        
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<CreateFormTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsCreatedWithFormTemplate()
    {
        // Arrange
        var request = new CreateFormTemplateRequest 
        { 
            Name = "Test Template",
            Description = "Test Description",
            IsEnabled = true,
            JsonData = "{ }"
        };
        
        var formTemplate = new FormTemplate(SampleData.TENANT_ID, request.Name!) { Id = 1 };
        var result = Result<FormTemplate>.Created(formTemplate);

        _mediator.Send(Arg.Any<CreateFormTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var createdResult = response.Result as Created<CreateFormTemplateResponse>;
        createdResult.Should().NotBeNull();
        createdResult!.Value.Should().NotBeNull();
        createdResult!.Value!.Id.Should().Be(formTemplate.Id.ToString());
        createdResult!.Value!.Name.Should().Be(formTemplate.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new CreateFormTemplateRequest
        {
            Name = "Test Template",
            Description = "Test Description",
            IsEnabled = true,
            JsonData = "{ }"
        };
        var result = Result<FormTemplate>.Created(new FormTemplate(SampleData.TENANT_ID, "Test Template"));
        
        _mediator.Send(Arg.Any<CreateFormTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateFormTemplateCommand>(cmd =>
                cmd.Name == request.Name &&
                cmd.Description == request.Description &&
                cmd.IsEnabled == request.IsEnabled &&
                cmd.JsonData == request.JsonData
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

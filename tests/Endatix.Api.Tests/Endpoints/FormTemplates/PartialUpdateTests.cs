using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormTemplates;
using Endatix.Core.UseCases.FormTemplates.PartialUpdate;

namespace Endatix.Api.Tests.Endpoints.FormTemplates;

public class PartialUpdateTests
{
    private readonly IMediator _mediator;
    private readonly PartialUpdate _endpoint;

    public PartialUpdateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<PartialUpdate>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formTemplateId = 1L;
        var request = new PartialUpdateFormTemplateRequest { FormTemplateId = formTemplateId };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<PartialUpdateFormTemplateCommand>(), Arg.Any<CancellationToken>())
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
        var request = new PartialUpdateFormTemplateRequest 
        { 
            FormTemplateId = formTemplateId,
            Name = "Updated Template",
            Description = "Updated Description",
            IsEnabled = true,
            JsonData = "{ }"
        };
        var result = Result.NotFound("Form template not found.");

        _mediator.Send(Arg.Any<PartialUpdateFormTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithUpdatedFormTemplate()
    {
        // Arrange
        var formTemplateId = 1L;
        var request = new PartialUpdateFormTemplateRequest 
        { 
            FormTemplateId = formTemplateId,
            Name = "Updated Template",
            Description = "Updated Description",
            IsEnabled = true,
            JsonData = "{ }"
        };
        
        var formTemplate = new FormTemplate(SampleData.TENANT_ID, request.Name!) { Id = formTemplateId };
        var result = Result.Success(formTemplate);

        _mediator.Send(Arg.Any<PartialUpdateFormTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<PartialUpdateFormTemplateResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(formTemplateId.ToString());
        okResult!.Value!.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new PartialUpdateFormTemplateRequest
        {
            FormTemplateId = 123,
            Name = "Updated Template",
            Description = "Updated Description",
            IsEnabled = true,
            JsonData = "{ }"
        };
        var result = Result.Success(new FormTemplate(SampleData.TENANT_ID, "Updated Template"));
        
        _mediator.Send(Arg.Any<PartialUpdateFormTemplateCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<PartialUpdateFormTemplateCommand>(cmd =>
                cmd.FormTemplateId == request.FormTemplateId &&
                cmd.Name == request.Name &&
                cmd.Description == request.Description &&
                cmd.IsEnabled == request.IsEnabled &&
                cmd.JsonData == request.JsonData
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

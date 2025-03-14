using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormTemplates;
using Endatix.Core.UseCases.FormTemplates.GetById;

namespace Endatix.Api.Tests.Endpoints.FormTemplates;

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
        var formTemplateId = 1L;
        var request = new GetFormTemplateByIdRequest { FormTemplateId = formTemplateId };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<GetFormTemplateByIdQuery>(), Arg.Any<CancellationToken>())
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
        var request = new GetFormTemplateByIdRequest { FormTemplateId = formTemplateId };
        var result = Result.NotFound("Form template not found.");

        _mediator.Send(Arg.Any<GetFormTemplateByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithFormTemplate()
    {
        // Arrange
        var formTemplateId = 1L;
        var request = new GetFormTemplateByIdRequest { FormTemplateId = formTemplateId };
        var formTemplate = new FormTemplate(SampleData.TENANT_ID, "Test Template") { Id = formTemplateId };
        var result = Result.Success(formTemplate);

        _mediator.Send(Arg.Any<GetFormTemplateByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<FormTemplateModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(formTemplateId.ToString());
        okResult!.Value!.Name.Should().Be(formTemplate.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new GetFormTemplateByIdRequest { FormTemplateId = 123 };
        var result = Result.Success(new FormTemplate(SampleData.TENANT_ID, "Test Template"));
        
        _mediator.Send(Arg.Any<GetFormTemplateByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetFormTemplateByIdQuery>(query =>
                query.FormTemplateId == request.FormTemplateId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

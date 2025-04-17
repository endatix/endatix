using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormDefinitions;
using Endatix.Core.UseCases.FormDefinitions.GetActive;
using Errors = Microsoft.AspNetCore.Mvc;
using Endatix.Core.UseCases.FormDefinitions;

namespace Endatix.Api.Tests.Endpoints.FormDefinitions;

public class GetActiveTests
{
    private readonly IMediator _mediator;
    private readonly GetActive _endpoint;

    public GetActiveTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetActive>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var request = new GetActiveFormDefinitionRequest { FormId = formId };
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<GetActiveFormDefinitionQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_FormDefinitionNotFound_ReturnsNotFound()
    {
        // Arrange
        var formId = 1L;
        var request = new GetActiveFormDefinitionRequest { FormId = formId };
        var result = Result.NotFound("Active form definition not found");

        _mediator.Send(Arg.Any<GetActiveFormDefinitionQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound<Errors.ProblemDetails>;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithFormDefinition()
    {
        // Arrange
        var formId = 1L;
        var request = new GetActiveFormDefinitionRequest { FormId = formId };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, true, "{ }") { Id = 1 };
        var themeJsonData = "{ \"background\": \"#FFFFFF\" }";
        var activeDefinitionDto = new ActiveDefinitionDto(formDefinition, themeJsonData);
        var result = Result.Success(activeDefinitionDto);

        _mediator.Send(Arg.Any<GetActiveFormDefinitionQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<FormDefinitionModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(formDefinition.Id.ToString());
        okResult!.Value!.FormId.Should().Be(formDefinition.FormId.ToString());
        okResult!.Value!.ThemeJsonData.Should().Be(themeJsonData);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToQueryCorrectly()
    {
        // Arrange
        var request = new GetActiveFormDefinitionRequest { FormId = 123 };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, true, "{ }");
        var themeJsonData = "{ \"background\": \"#FFFFFF\" }";
        var activeDefinitionDto = new ActiveDefinitionDto(formDefinition, themeJsonData);
        var result = Result.Success(activeDefinitionDto);
        
        _mediator.Send(Arg.Any<GetActiveFormDefinitionQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetActiveFormDefinitionQuery>(query =>
                query.FormId == request.FormId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

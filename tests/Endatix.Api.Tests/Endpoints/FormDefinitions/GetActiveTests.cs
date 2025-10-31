using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormDefinitions;
using Endatix.Core.UseCases.FormDefinitions.GetActive;
using Errors = Microsoft.AspNetCore.Mvc;
using Endatix.Core.UseCases.FormDefinitions;
using Endatix.Core.Abstractions;

namespace Endatix.Api.Tests.Endpoints.FormDefinitions;

public class GetActiveTests
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;
    private readonly GetActive _endpoint;

    public GetActiveTests()
    {
        _mediator = Substitute.For<IMediator>();
        _userContext = Substitute.For<IUserContext>();
        _endpoint = Factory.Create<GetActive>(_mediator, _userContext);
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
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
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
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
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
        okResult!.Value!.ThemeModel.Should().Be(themeJsonData);
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

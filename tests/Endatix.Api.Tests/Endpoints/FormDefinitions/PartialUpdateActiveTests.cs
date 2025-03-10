using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.FormDefinitions;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;
using Endatix.Api.Tests.TestUtils;

namespace Endatix.Api.Tests.Endpoints.FormDefinitions;

public class PartialUpdateActiveTests
{
    private readonly IMediator _mediator;
    private readonly PartialUpdateActive _endpoint;

    public PartialUpdateActiveTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<PartialUpdateActive>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var request = new PartialUpdateActiveFormDefinitionRequest { FormId = formId };
        var result = Result.Invalid();

        _mediator.Send(Arg.Any<PartialUpdateActiveFormDefinitionCommand>(), Arg.Any<CancellationToken>())
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
        var request = new PartialUpdateActiveFormDefinitionRequest { FormId = formId };
        var result = Result.NotFound("Active form definition not found");

        _mediator.Send(Arg.Any<PartialUpdateActiveFormDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var notFoundResult = response.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithUpdatedFormDefinition()
    {
        // Arrange
        var formId = 1L;
        var formDefinitionId = 2L;
        var isDraft = false;
        var request = new PartialUpdateActiveFormDefinitionRequest
        {
            FormId = formId,
            IsDraft = false,
            JsonData = "{ }"
        };

        var formDefinition = FormDefinitionFactory.CreateForTesting(isDraft, "{ }", formId, formDefinitionId);
        var result = Result.Success(formDefinition);

        _mediator.Send(Arg.Any<PartialUpdateActiveFormDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<PartialUpdateActiveFormDefinitionResponse>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult!.Value!.Id.Should().Be(formDefinition.Id.ToString());
        okResult!.Value!.FormId.Should().Be(formDefinition.FormId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new PartialUpdateActiveFormDefinitionRequest
        {
            FormId = 123,
            IsDraft = false,
            JsonData = "{ }"
        };
        var result = Result.Success(new FormDefinition(SampleData.TENANT_ID, false, "{ }"));

        _mediator.Send(Arg.Any<PartialUpdateActiveFormDefinitionCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<PartialUpdateActiveFormDefinitionCommand>(cmd =>
                cmd.FormId == request.FormId &&
                cmd.IsDraft == request.IsDraft &&
                cmd.JsonData == request.JsonData
            ),
            Arg.Any<CancellationToken>()
        );
    }
}

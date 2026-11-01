using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Endpoints.Forms;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Forms;

public sealed class AddSearchKeyTests
{
    private const long FormId = 100;

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly AddSearchKey _endpoint;

    public AddSearchKeyTests()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _endpoint = Factory.Create<AddSearchKey>(_mediator, tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_KeyAdded_ReturnsTheFormSearchKeys()
    {
        // Arrange
        SubmissionGridSearchKeys searchKeys = new(FormId, [], new SubmissionGridSearchSlots(1, 64, 1, 14));
        _mediator.Send(Arg.Any<AddSearchKeyCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(searchKeys));

        // Act
        var response = await _endpoint.ExecuteAsync(new AddSearchKeyRequest { FormId = FormId, Key = "q1" }, CancellationToken.None);

        // Assert
        var ok = response.Result as Ok<SubmissionGridSearchKeys>;
        ok.Should().NotBeNull();
        ok!.Value.Should().Be(searchKeys);
    }

    [Fact]
    public async Task ExecuteAsync_Request_SendsTheCallersTenantFormAndKey()
    {
        // Arrange
        _mediator.Send(Arg.Any<AddSearchKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmissionGridSearchKeys>.NotFound());

        // Act
        await _endpoint.ExecuteAsync(new AddSearchKeyRequest { FormId = FormId, Key = "q1" }, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<AddSearchKeyCommand>(command => command.Target == new SearchKeyTarget(SampleData.TENANT_ID, FormId, "q1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_OverTheCap_ReturnsBadRequest()
    {
        // Arrange
        _mediator.Send(Arg.Any<AddSearchKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmissionGridSearchKeys>.Invalid(new ValidationError("key", FormSearchKeys.CapMessage, "SearchKeysCap", ValidationSeverity.Error)));

        // Act
        var response = await _endpoint.ExecuteAsync(new AddSearchKeyRequest { FormId = FormId, Key = "q1" }, CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult.ProblemDetails.Detail.Should().Contain("14");
    }

    [Fact]
    public async Task ExecuteAsync_FormNotFound_ReturnsNotFound()
    {
        // Arrange
        _mediator.Send(Arg.Any<AddSearchKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmissionGridSearchKeys>.NotFound("Form not found."));

        // Act
        var response = await _endpoint.ExecuteAsync(new AddSearchKeyRequest { FormId = FormId, Key = "q1" }, CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

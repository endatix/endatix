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

public sealed class RemoveSearchKeyTests
{
    private const long FormId = 100;

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly RemoveSearchKey _endpoint;

    public RemoveSearchKeyTests()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _endpoint = Factory.Create<RemoveSearchKey>(_mediator, tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_KeyRemoved_ReturnsTheFormSearchKeys()
    {
        // Arrange
        SubmissionGridSearchKeys searchKeys = new(FormId, [], new SubmissionGridSearchSlots(0, 64, 0, 14));
        _mediator.Send(Arg.Is<RemoveSearchKeyCommand>(command => command.Target == new SearchKeyTarget(SampleData.TENANT_ID, FormId, "q1")),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(searchKeys));

        // Act
        var response = await _endpoint.ExecuteAsync(new RemoveSearchKeyRequest { FormId = FormId, Key = "q1" }, CancellationToken.None);

        // Assert
        var ok = response.Result as Ok<SubmissionGridSearchKeys>;
        ok.Should().NotBeNull();
        ok!.Value.Should().Be(searchKeys);
    }

    [Fact]
    public async Task ExecuteAsync_AutomaticKey_ReturnsBadRequest()
    {
        // Arrange
        _mediator.Send(Arg.Any<RemoveSearchKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmissionGridSearchKeys>.Invalid(new ValidationError("key", FormSearchKeys.AutomaticKeyMessage, "AutomaticSearchKey", ValidationSeverity.Error)));

        // Act
        var response = await _endpoint.ExecuteAsync(new RemoveSearchKeyRequest { FormId = FormId, Key = "q1" }, CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_KeyNotSearchable_ReturnsNotFound()
    {
        // Arrange
        _mediator.Send(Arg.Any<RemoveSearchKeyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SubmissionGridSearchKeys>.NotFound(FormSearchKeys.NotSearchableMessage));

        // Act
        var response = await _endpoint.ExecuteAsync(new RemoveSearchKeyRequest { FormId = FormId, Key = "q1" }, CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.DeleteTests;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public sealed class DeleteTestsTests
{
    private const long TenantId = 7;

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly DeleteTests _endpoint;

    public DeleteTestsTests()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId);
        _endpoint = Factory.Create<DeleteTests>(_mediator, tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsDeletedCount()
    {
        // Arrange
        _mediator.Send(Arg.Is<DeleteTestSubmissionsCommand>(c => c.TenantId == TenantId && c.FormId == 1), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new DeletedTestSubmissions(3)));

        // Act
        var response = await _endpoint.ExecuteAsync(new DeleteTestSubmissionsRequest { FormId = 1 }, CancellationToken.None);

        // Assert
        var ok = response.Result.Should().BeOfType<Ok<DeleteTestSubmissionsResponse>>().Subject;
        ok.Value!.DeletedCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_Forbidden_Returns403Problem()
    {
        // Arrange
        _mediator.Send(Arg.Any<DeleteTestSubmissionsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<DeletedTestSubmissions>.Forbidden());

        // Act
        var response = await _endpoint.ExecuteAsync(new DeleteTestSubmissionsRequest { FormId = 1 }, CancellationToken.None);

        // Assert
        var problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ExecuteAsync_FormNotFound_Returns404Problem()
    {
        // Arrange
        _mediator.Send(Arg.Any<DeleteTestSubmissionsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<DeletedTestSubmissions>.NotFound("Form not found"));

        // Act
        var response = await _endpoint.ExecuteAsync(new DeleteTestSubmissionsRequest { FormId = 1 }, CancellationToken.None);

        // Assert
        var problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

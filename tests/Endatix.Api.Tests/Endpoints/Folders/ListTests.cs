using Endatix.Api.Endpoints.Folders;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Folders;

public sealed class ListTests
{
    [Theory]
    [InlineData(false, 403)]
    [InlineData(true, 500)]
    public async Task ExecuteAsync_InactivePermissionNotGranted_ReturnsOrdinaryProblemWithoutDispatch(bool serviceError, int status)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var permissions = Substitute.For<ICurrentUserAuthorizationService>();
        var mediator = Substitute.For<IMediator>();
        var decision = serviceError ? Result<bool>.Error("Permission lookup failed.") : Result.Success(false);
        permissions.HasPermissionAsync(Actions.Folders.Manage, ct).Returns(decision);
        var endpoint = Factory.Create<List>(permissions, mediator);

        // Act
        var response = await endpoint.ExecuteAsync(new ListFoldersRequest { IncludeInactive = true }, ct);

        // Assert
        var problem = Assert.IsType<ProblemHttpResult>(response.Result);
        Assert.Equal(status, problem.StatusCode);
        Assert.Empty(mediator.ReceivedCalls());
    }
}

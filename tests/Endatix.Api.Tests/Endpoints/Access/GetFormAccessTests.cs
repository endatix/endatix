using System.Collections.Immutable;
using Endatix.Api.Endpoints.Access;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ResourcePermissions = Endatix.Core.Authorization.Access.ResourcePermissions;

namespace Endatix.Api.Tests.Endpoints.Access;

public class GetFormAccessTests
{
    private readonly IResourceAccessQuery<FormAccessData, FormAccessContext> _accessPolicy;
    private readonly GetFormAccess _endpoint;

    public GetFormAccessTests()
    {
        _accessPolicy = Substitute.For<IResourceAccessQuery<FormAccessData, FormAccessContext>>();
        _endpoint = Factory.Create<GetFormAccess>(_accessPolicy);
    }

    [Fact]
    public void Endpoint_CanBeCreated()
    {
        _endpoint.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulAccess_ReturnsOkResult()
    {
        // Arrange
        var request = new GetFormAccessRequest { FormId = 123 };

        var accessData = new FormAccessData
        {
            FormId = request.FormId.ToString(),
            Permissions = ResourcePermissions.Form.Sets.ViewForm.ToImmutableHashSet()
        };

        var cached = new Cached<FormAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(10), "etag-123");

        _accessPolicy
            .GetAccessData(Arg.Any<FormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<ICachedData<FormAccessData>>.Success(cached));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<GetFormAccessResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value!.FormId.Should().Be(accessData.FormId);
        okResult.Value!.Permissions.Should().BeEquivalentTo(accessData.Permissions);
        okResult.Value!.ETag.Should().Be(cached.ETag);
        okResult.Value!.CachedAt.Should().Be(cached.CachedAt);
        okResult.Value!.ExpiresAt.Should().Be(cached.ExpiresAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnauthorized_ReturnsProblemResult()
    {
        // Arrange
        var request = new GetFormAccessRequest { FormId = 123 };

        var errorResult = Result<ICachedData<FormAccessData>>.Unauthorized("You are not authorized to access this form.");
        _accessPolicy
            .GetAccessData(Arg.Any<FormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        problemResult.ProblemDetails.Detail.Should().Contain("You are not authorized");
    }

    [Fact]
    public async Task ExecuteAsync_WithForbidden_ReturnsProblemResult()
    {
        // Arrange
        var request = new GetFormAccessRequest { FormId = 123 };

        var errorResult = Result<ICachedData<FormAccessData>>.Forbidden("You are not authorized to access this form.");
        _accessPolicy
            .GetAccessData(Arg.Any<FormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        problemResult.ProblemDetails.Detail.Should().Contain("You are not authorized");
    }

    [Fact]
    public async Task ExecuteAsync_WithUnexpectedError_ReturnsProblemResult()
    {
        // Arrange
        var request = new GetFormAccessRequest { FormId = 123 };

        var errorResult = Result<ICachedData<FormAccessData>>.Error("unexpected error");
        _accessPolicy
            .GetAccessData(Arg.Any<FormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        problemResult.ProblemDetails.Detail.Should().Contain("unexpected error");
    }

    [Fact]
    public async Task ExecuteAsync_WithViewAccess_ReturnsPermissionsWithViewOnly()
    {
        // Arrange
        var formId = 456L;
        var request = new GetFormAccessRequest { FormId = formId };

        var accessData = FormAccessData.CreateWithViewAccess(formId);
        var cached = new Cached<FormAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(5), "etag-view");

        _accessPolicy
            .GetAccessData(Arg.Is<FormAccessContext>(c => c.FormId == formId), Arg.Any<CancellationToken>())
            .Returns(Result<ICachedData<FormAccessData>>.Success(cached));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<GetFormAccessResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value!.FormId.Should().Be(formId.ToString());
        okResult.Value!.Permissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.ViewForm);
    }

    [Fact]
    public async Task ExecuteAsync_WithEditAccess_ReturnsPermissionsWithEditAccess()
    {
        // Arrange
        var formId = 789L;
        var request = new GetFormAccessRequest { FormId = formId };

        var accessData = FormAccessData.CreateWithEditAccess(formId);
        var cached = new Cached<FormAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(5), "etag-edit");

        _accessPolicy
            .GetAccessData(Arg.Is<FormAccessContext>(c => c.FormId == formId), Arg.Any<CancellationToken>())
            .Returns(Result<ICachedData<FormAccessData>>.Success(cached));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<GetFormAccessResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value!.FormId.Should().Be(formId.ToString());
        okResult.Value!.Permissions.Should().BeEquivalentTo(ResourcePermissions.Form.Sets.EditForm);
    }
}

using Endatix.Api.Endpoints.Access;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ResourcePermissions = Endatix.Core.Authorization.Access.ResourcePermissions;

namespace Endatix.Api.Tests.Endpoints.Access;

public class GetFormTemplateAccessTests
{
    private readonly IResourceAccessQuery<FormTemplateAccessData, FormTemplateAccessContext> _accessPolicy;
    private readonly GetFormTemplateAccess _endpoint;

    public GetFormTemplateAccessTests()
    {
        _accessPolicy = Substitute.For<IResourceAccessQuery<FormTemplateAccessData, FormTemplateAccessContext>>();
        _endpoint = Factory.Create<GetFormTemplateAccess>(_accessPolicy);
    }

    [Fact]
    public void Endpoint_CanBeCreated()
        => _endpoint.Should().NotBeNull();

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulAccess_ReturnsOkResult()
    {
        // Arrange
        var templateId = 222L;
        var request = new GetFormTemplateAccessRequest { TemplateId = templateId };

        var accessData = new FormTemplateAccessData
        {
            TemplateId = request.TemplateId.ToString(),
            Permissions = [.. ResourcePermissions.Template.Sets.EditTemplate]
        };

        var cached = new Cached<FormTemplateAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(10), "etag-123");

        _accessPolicy
            .GetAccessData(Arg.Any<FormTemplateAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<Cached<FormTemplateAccessData>>.Success(cached));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<GetFormTemplateAccessResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value!.TemplateId.Should().Be(accessData.TemplateId);
        okResult.Value!.Permissions.Should().BeEquivalentTo(accessData.Permissions);
        okResult.Value!.CachedAt.Should().Be(cached.CachedAt);
        okResult.Value!.ExpiresAt.Should().Be(cached.ExpiresAt);
        okResult.Value!.ETag.Should().Be(cached.ETag);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnauthorized_ReturnsProblemResult()
    {
        // Arrange
        var request = new GetFormTemplateAccessRequest { TemplateId = 123L };

        var errorResult = Result<Cached<FormTemplateAccessData>>.Unauthorized("You are not authorized to access this form template.");
        _accessPolicy
            .GetAccessData(Arg.Any<FormTemplateAccessContext>(), Arg.Any<CancellationToken>())
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
        var request = new GetFormTemplateAccessRequest { TemplateId = 123L };

        var errorResult = Result<Cached<FormTemplateAccessData>>.Forbidden("You are not authorized to access this form template.");
        _accessPolicy
            .GetAccessData(Arg.Any<FormTemplateAccessContext>(), Arg.Any<CancellationToken>())
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
        var request = new GetFormTemplateAccessRequest { TemplateId = 123L };

        var errorResult = Result<Cached<FormTemplateAccessData>>.Error("unexpected error");
        _accessPolicy
            .GetAccessData(Arg.Any<FormTemplateAccessContext>(), Arg.Any<CancellationToken>())
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
        var templateId = 456L;
        var request = new GetFormTemplateAccessRequest { TemplateId = templateId };

        var accessData = FormTemplateAccessData.CreateWithViewAccess(templateId);
        var cached = new Cached<FormTemplateAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(5), "etag-view");

        _accessPolicy
            .GetAccessData(Arg.Is<FormTemplateAccessContext>(c => c.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns(Result<Cached<FormTemplateAccessData>>.Success(cached));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<GetFormTemplateAccessResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value!.TemplateId.Should().Be(templateId.ToString());
        okResult.Value!.Permissions.Should().BeEquivalentTo(ResourcePermissions.Template.Sets.ViewTemplate);
    }

    [Fact]
    public async Task ExecuteAsync_WithEditAccess_ReturnsPermissionsWithEditAccess()
    {
        // Arrange
        var templateId = 789L;
        var request = new GetFormTemplateAccessRequest { TemplateId = templateId };

        var accessData = FormTemplateAccessData.CreateWithEditAccess(templateId);
        var cached = new Cached<FormTemplateAccessData>(accessData, DateTime.UtcNow, TimeSpan.FromMinutes(5), "etag-edit");

        _accessPolicy
            .GetAccessData(Arg.Is<FormTemplateAccessContext>(c => c.TemplateId == templateId), Arg.Any<CancellationToken>())
            .Returns(Result<Cached<FormTemplateAccessData>>.Success(cached));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result.As<Ok<GetFormTemplateAccessResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value!.TemplateId.Should().Be(templateId.ToString());
        okResult.Value!.Permissions.Should().BeEquivalentTo(ResourcePermissions.Template.Sets.EditTemplate);
    }
}


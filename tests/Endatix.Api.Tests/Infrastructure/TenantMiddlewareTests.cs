using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Endatix.Api.Tests.Infrastructure;

public sealed class TenantMiddlewareTests
{
    private const string REQUEST_PATH = "/api/forms";

    private readonly RequestDelegate _next = Substitute.For<RequestDelegate>();
    private readonly ILogger<TenantMiddleware> _logger = Substitute.For<ILogger<TenantMiddleware>>();
    private readonly EndatixJwtOptions _jwtOptions = new();
    private readonly TenantContext _tenantContext = new();
    private readonly TenantMiddleware _middleware;

    public TenantMiddlewareTests()
    {
        _middleware = new TenantMiddleware(_next, _logger);
    }

    [Fact]
    public async Task InvokeAsync_Anonymous_CallsNextWithoutTenant()
    {
        // Arrange
        var httpContext = CreateHttpContext(new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        await InvokeAsync(httpContext);

        // Assert
        _tenantContext.TenantId.Should().Be(0);
        await _next.Received(1).Invoke(httpContext);
    }

    [Fact]
    public async Task InvokeAsync_LocalJwtWithTenant_SetsTenantFromToken()
    {
        // Arrange
        var httpContext = CreateHttpContext(CreatePrincipal(_jwtOptions.Issuer, tenantId: "3"));

        // Act
        await InvokeAsync(httpContext);

        // Assert
        _tenantContext.TenantId.Should().Be(3);
        await _next.Received(1).Invoke(httpContext);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("not-a-number")]
    public async Task InvokeAsync_LocalJwtWithoutValidTenant_ReturnsForbidden(string? tenantId)
    {
        // Arrange
        var httpContext = CreateHttpContext(CreatePrincipal(_jwtOptions.Issuer, tenantId));

        // Act
        await InvokeAsync(httpContext);

        // Assert
        await AssertForbiddenAsync(httpContext);
    }

    [Fact]
    public async Task InvokeAsync_SignedInWithUnexpectedTenantContext_EndsTheRequestWithServerError()
    {
        // Arrange
        var httpContext = CreateHttpContext(CreatePrincipal(_jwtOptions.Issuer, tenantId: "3"));
        var unexpectedTenantContext = Substitute.For<ITenantContext>();

        // Act
        await _middleware.InvokeAsync(httpContext, unexpectedTenantContext, Options.Create(_jwtOptions));

        // Assert
        await _next.DidNotReceiveWithAnyArgs().Invoke(default!);
        await AssertProblemAsync(httpContext, new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://www.rfc-editor.org/rfc/rfc9110.html#name-500-internal-server-error",
            Title = "An unexpected error occurred",
            Detail = "An unexpected error occurred",
        });
    }

    private Task InvokeAsync(HttpContext httpContext) =>
        _middleware.InvokeAsync(httpContext, _tenantContext, Options.Create(_jwtOptions));

    private async Task AssertForbiddenAsync(HttpContext httpContext)
    {
        _tenantContext.TenantId.Should().Be(0);
        await _next.DidNotReceiveWithAnyArgs().Invoke(default!);
        await AssertProblemAsync(httpContext, new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Type = "https://www.rfc-editor.org/rfc/rfc9110.html#name-403-forbidden",
            Title = "Forbidden access",
            Detail = "The tenant for this request could not be resolved.",
        });
    }

    private static async Task AssertProblemAsync(HttpContext httpContext, ProblemDetails expected)
    {
        httpContext.Response.StatusCode.Should().Be(expected.Status);
        httpContext.Response.ContentType.Should().Be("application/problem+json");

        httpContext.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(httpContext.Response.Body);
        var root = problem.RootElement;
        root.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo("type", "title", "status", "detail", "instance", "traceId");
        root.GetProperty("type").GetString().Should().Be(expected.Type);
        root.GetProperty("title").GetString().Should().Be(expected.Title);
        root.GetProperty("status").GetInt32().Should().Be(expected.Status);
        root.GetProperty("detail").GetString().Should().Be(expected.Detail);
        root.GetProperty("instance").GetString().Should().Be(REQUEST_PATH);
        root.GetProperty("traceId").GetString().Should().Be(Activity.Current?.Id ?? httpContext.TraceIdentifier);
    }

    private static DefaultHttpContext CreateHttpContext(ClaimsPrincipal user)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            User = user,
            RequestServices = services,
            Request = { Path = REQUEST_PATH },
            Response = { Body = new MemoryStream() }
        };
    }

    private static ClaimsPrincipal CreatePrincipal(string issuer, string? tenantId)
    {
        var identity = new ClaimsIdentity(authenticationType: "Bearer");
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, issuer));
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, "123"));
        if (tenantId is not null)
        {
            identity.AddClaim(new Claim(ClaimNames.TenantId, tenantId));
        }

        return new ClaimsPrincipal(identity);
    }
}

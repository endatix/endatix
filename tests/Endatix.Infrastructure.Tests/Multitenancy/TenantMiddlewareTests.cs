using System.Security.Claims;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Endatix.Infrastructure.Tests.Multitenancy;

public sealed class TenantMiddlewareTests
{
    private const string ExternalIssuer = "https://keycloak.test/realms/endatix";

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

    [Fact]
    public async Task InvokeAsync_ReBacJwtWithTenant_SetsTenantFromToken()
    {
        // Arrange
        var httpContext = CreateHttpContext(CreatePrincipal(_jwtOptions.ReBacIssuer, tenantId: "42"));

        // Act
        await InvokeAsync(httpContext);

        // Assert
        _tenantContext.TenantId.Should().Be(42);
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
    public async Task InvokeAsync_ExternalIssuerHydratedWithTenant_SetsTenantFromAuthorizedIdentity()
    {
        // Arrange
        var principal = CreatePrincipal(ExternalIssuer, tenantId: null);
        principal.AddIdentity(CreateAuthorizedIdentity(tenantId: 7));
        var httpContext = CreateHttpContext(principal);

        // Act
        await InvokeAsync(httpContext);

        // Assert
        _tenantContext.TenantId.Should().Be(7);
        await _next.Received(1).Invoke(httpContext);
    }

    [Fact]
    public async Task InvokeAsync_ExternalIssuerHydratedWithTenant_IgnoresTheIssuersOwnTenantClaim()
    {
        // Arrange
        var principal = CreatePrincipal(ExternalIssuer, tenantId: "5");
        principal.AddIdentity(CreateAuthorizedIdentity(tenantId: 7));
        var httpContext = CreateHttpContext(principal);

        // Act
        await InvokeAsync(httpContext);

        // Assert
        _tenantContext.TenantId.Should().Be(7);
    }

    [Fact]
    public async Task InvokeAsync_ExternalIssuerHydratedAtTenantZero_ReturnsForbidden()
    {
        // Arrange
        var principal = CreatePrincipal(ExternalIssuer, tenantId: "5");
        principal.AddIdentity(CreateAuthorizedIdentity(tenantId: 0));
        var httpContext = CreateHttpContext(principal);

        // Act
        await InvokeAsync(httpContext);

        // Assert
        await AssertForbiddenAsync(httpContext);
    }

    [Fact]
    public async Task InvokeAsync_ExternalIssuerNotHydrated_ReturnsForbidden()
    {
        // Arrange
        var httpContext = CreateHttpContext(CreatePrincipal(ExternalIssuer, tenantId: "5"));

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
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        await _next.DidNotReceiveWithAnyArgs().Invoke(default!);
    }

    private Task InvokeAsync(HttpContext httpContext) =>
        _middleware.InvokeAsync(httpContext, _tenantContext, Options.Create(_jwtOptions));

    private async Task AssertForbiddenAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
        _tenantContext.TenantId.Should().Be(0);
        await _next.DidNotReceiveWithAnyArgs().Invoke(default!);

        httpContext.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(httpContext.Response.Body);
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status403Forbidden);
        problem.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
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

    private static AuthorizedIdentity CreateAuthorizedIdentity(long tenantId) =>
        new(AuthorizationData.ForAuthenticatedUser(
            userId: "123",
            tenantId: tenantId,
            roles: [],
            permissions: []));
}

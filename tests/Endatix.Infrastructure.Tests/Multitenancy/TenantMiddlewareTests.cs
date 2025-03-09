using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using FluentAssertions;
using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Multitenancy;

namespace Endatix.Infrastructure.Tests.Multitenancy;

public class TenantMiddlewareTests
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;
    private readonly HttpContext _httpContext;
    private readonly ITenantContext _tenantContext;
    private readonly TenantMiddleware _middleware;
    private readonly ClaimsPrincipal _user;

    public TenantMiddlewareTests()
    {
        _next = Substitute.For<RequestDelegate>();
        _logger = Substitute.For<ILogger<TenantMiddleware>>();
        _httpContext = Substitute.For<HttpContext>();
        _tenantContext = Substitute.For<TenantContext>();
        _user = Substitute.For<ClaimsPrincipal>();
        _httpContext.User.Returns(_user);
        _middleware = new TenantMiddleware(_next, _logger);
    }

    [Fact]
    public async Task InvokeAsync_NoTenantClaim_CallsNextMiddleware()
    {
        // Arrange
        _user.FindFirst(ClaimNames.TenantId).Returns((Claim?)null);

        // Act
        await _middleware.InvokeAsync(_httpContext, _tenantContext);

        // Assert
        await _next.Received(1).Invoke(_httpContext);
    }

    [Fact]
    public async Task InvokeAsync_InvalidTenantIdFormat_LogsErrorAndCallsNextMiddleware()
    {
        // Arrange
        var invalidTenantId = "invalid";
        var tenantClaim = new Claim(ClaimNames.TenantId, invalidTenantId);
        _user.FindFirst(ClaimNames.TenantId).Returns(tenantClaim);

        // Act
        await _middleware.InvokeAsync(_httpContext, _tenantContext);

        // Assert
        _logger.Received().Log(
            LogLevel.Error,
            0,
            Arg.Is<object>(o => o.ToString().Contains($"Invalid tenant ID format: {invalidTenantId}")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
        await _next.Received(1).Invoke(_httpContext);
    }

    [Fact]
    public async Task InvokeAsync_InvalidTenantContextType_LogsErrorAndCallsNextMiddleware()
    {
        // Arrange
        var tenantId = "123";
        var tenantClaim = new Claim(ClaimNames.TenantId, tenantId);
        _user.FindFirst(ClaimNames.TenantId).Returns(tenantClaim);
        var invalidTenantContext = Substitute.For<ITenantContext>();

        // Act
        await _middleware.InvokeAsync(_httpContext, invalidTenantContext);

        // Assert
        _logger.Received().Log(
            LogLevel.Error,
            0,
            Arg.Is<object>(o => o.ToString().Contains($"Expected TenantContext but got {invalidTenantContext.GetType().Name}")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
        await _next.Received(1).Invoke(_httpContext);
    }

    [Fact]
    public async Task InvokeAsync_ValidTenantClaim_SetsTenantIdAndCallsNextMiddleware()
    {
        // Arrange
        var tenantId = "123";
        var tenantClaim = new Claim(ClaimNames.TenantId, tenantId);
        _user.FindFirst(ClaimNames.TenantId).Returns(tenantClaim);

        // Act
        await _middleware.InvokeAsync(_httpContext, _tenantContext);

        // Assert
        ((TenantContext)_tenantContext).Received(1).SetTenant(123);
        await _next.Received(1).Invoke(_httpContext);
    }
}

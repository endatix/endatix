using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Users;
using Endatix.Infrastructure.Data;
using Endatix.Core.Abstractions;
using FluentAssertions;
using System.Security.Claims;
using Endatix.Core.Entities.Identity;

namespace Endatix.Infrastructure.Tests.Identity.Users;

public class AppUserServiceTests
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITenantContext _tenantContext;
    private readonly AppIdentityDbContext _identityDbContext;
    private readonly AppUserService _userService;

    public AppUserServiceTests()
    {
        _userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(),
            null, null, null, null, null, null, null, null);
        _tenantContext = Substitute.For<ITenantContext>();
        var dbOptions = new DbContextOptionsBuilder<AppIdentityDbContext>().Options;
        var valueGeneratorFactory = Substitute.For<EfCoreValueGeneratorFactory>(Substitute.For<IIdGenerator<long>>());
        var idGenerator = Substitute.For<IIdGenerator<long>>();
        _identityDbContext = Substitute.For<AppIdentityDbContext>(dbOptions, valueGeneratorFactory, idGenerator);
        _userService = new AppUserService(_userManager, _tenantContext, _identityDbContext);
    }

    [Fact]
    public async Task GetUserAsync_NullClaimsPrincipal_ReturnsNotFound()
    {
        // Act
        ClaimsPrincipal? nullClaimsPrincipal = null;
        var result = await _userService.GetUserAsync(nullClaimsPrincipal!, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetUserAsync_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _userManager.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(null as AppUser);

        // Act
        var result = await _userService.GetUserAsync(claimsPrincipal, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetUserAsync_UserFound_ReturnsSuccess()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var appUser = new AppUser { 
            Id = 22_111_111_111_111_111,
            TenantId = 1,
            UserName = "test@example.com",
            Email = "test@example.com"
        };
        _userManager.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(appUser);

        // Act
        var result = await _userService.GetUserAsync(claimsPrincipal, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(appUser.Id);
        result.Value.TenantId.Should().Be(appUser.TenantId);
    }
}

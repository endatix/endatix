using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Users;
using Endatix.Infrastructure.Data;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using System.Security.Claims;
using Endatix.Infrastructure.Data.Querying;

namespace Endatix.Infrastructure.Tests.Identity.Users;

public class AppUserServiceTests
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITenantContext _tenantContext;
    private readonly AppIdentityDbContext _identityDbContext;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IUserContext _userContext;
    private readonly IRelationalSubstringLikeFilter _substringLikeFilter;
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
        _emailVerificationService = Substitute.For<IEmailVerificationService>();
        _userContext = Substitute.For<IUserContext>();
        _substringLikeFilter = Substitute.For<IRelationalSubstringLikeFilter>();
        _userService = new AppUserService(
            _userManager,
            _tenantContext,
            _identityDbContext,
            _emailVerificationService,
            _userContext,
            _substringLikeFilter);
    }

    [Fact]
    public async Task ListUsersAsync_NegativeSkip_ReturnsInvalid()
    {
        // Act
        var result = await _userService.ListUsersAsync(-1, 10, null, null, null, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Skip must be greater than or equal to zero.");
    }

    [Fact]
    public async Task ListUsersAsync_ZeroTake_ReturnsInvalid()
    {
        // Act
        var result = await _userService.ListUsersAsync(0, 0, null, null, null, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Take must be greater than zero.");
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
        var appUser = new AppUser
        {
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

    [Fact]
    public async Task GetUserAsync_EmailWithWhitespace_UsesTrimmedEmail()
    {
        // Arrange
        var email = "test@example.com";
        var appUser = new AppUser
        {
            Id = 22_111_111_111_111_111,
            TenantId = 1,
            UserName = email,
            Email = email
        };
        _userManager.FindByEmailAsync(email).Returns(appUser);

        // Act
        var result = await _userService.GetUserAsync($" {email} ", TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(Core.Infrastructure.Result.ResultStatus.Ok);
        await _userManager.Received(1).FindByEmailAsync(email);
    }

    [Fact]
    public void CurrentTenantRoleAssignments_ReturnsCurrentTenantAndNonPlatformSystemAssignmentsForTargetUser()
    {
        // Arrange
        const long userId = 123;
        const long otherUserId = 456;
        const long currentTenantId = 10;
        const long otherTenantId = 20;

        var userRoles = new List<IdentityUserRole<long>>
        {
            new() { UserId = userId, RoleId = 1 },
            new() { UserId = userId, RoleId = 2 },
            new() { UserId = userId, RoleId = 3 },
            new() { UserId = userId, RoleId = 4 },
            new() { UserId = otherUserId, RoleId = 1 }
        }.AsQueryable();

        var roles = new List<AppRole>
        {
            new() { Id = 1, Name = "Custom", TenantId = currentTenantId },
            new() { Id = 2, Name = "OtherTenant", TenantId = otherTenantId },
            new() { Id = 3, Name = SystemRole.Admin.Name, TenantId = 0, IsSystemDefined = true },
            new() { Id = 4, Name = SystemRole.PlatformAdmin.Name, TenantId = 0, IsSystemDefined = true }
        }.AsQueryable();

        // Act
        var removableRoleIds = userRoles
            .CurrentTenantRoleAssignments(roles, userId, currentTenantId)
            .Select(userRole => userRole.RoleId)
            .ToList();

        // Assert
        removableRoleIds.Should().Equal([1, 3]);
    }
}

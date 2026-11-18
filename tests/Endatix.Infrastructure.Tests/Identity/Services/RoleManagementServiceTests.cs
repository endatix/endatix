using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Repositories;
using Endatix.Infrastructure.Identity.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Identity.Services;

public class RoleManagementServiceTests
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppIdentityDbContext _identityDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRolesRepository _rolesRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserAuthorizationService _currentUserAuthorizationService;
    private readonly IAuthorizationCache _authorizationCache;
    private readonly ILogger<RoleManagementService> _logger;
    private readonly RoleManagementService _service;
    private const long CALLER_ID = 42;

    public RoleManagementServiceTests()
    {
        _userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(),
            null, null, null, null, null, null, null, null);

        var dbOptions = new DbContextOptionsBuilder<AppIdentityDbContext>()
            .Options;

        _identityDbContext = Substitute.For<AppIdentityDbContext>(dbOptions);

        _tenantContext = Substitute.For<ITenantContext>();

        _rolesRepository = Substitute.For<IRolesRepository>();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _currentUserAuthorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _authorizationCache = Substitute.For<IAuthorizationCache>();
        _logger = Substitute.For<ILogger<RoleManagementService>>();

        _service = new RoleManagementService(
            _userManager,
            _identityDbContext,
            _tenantContext,
            _rolesRepository,
            _httpContextAccessor,
            _currentUserAuthorizationService,
            _authorizationCache,
            _logger);
    }

    #region AssignRoleToUserAsync Tests

    [Fact]
    public async Task AssignRoleToUserAsync_InvalidUserId_ReturnsInvalidResult()
    {
        // Act
        var result = await _service.AssignRoleToUserAsync(0, "Admin", CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("User ID must be greater than zero.");
        await _userManager.DidNotReceiveWithAnyArgs().FindByIdAsync(default!);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_BlankRoleName_ReturnsInvalidResult()
    {
        // Act
        var result = await _service.AssignRoleToUserAsync(1, " ", CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Role name is required.");
        await _userManager.DidNotReceiveWithAnyArgs().FindByIdAsync(default!);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_UserNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        SignInAs(CALLER_ID, SystemRole.Admin.Name);
        var userId = 999L;
        var roleName = "Admin";
        _userManager.FindByIdAsync(userId.ToString()).Returns((AppUser?)null);

        // Act
        var result = await _service.AssignRoleToUserAsync(userId, roleName, CancellationToken.None);

        // Assert
        result.IsNotFound().Should().BeTrue();
        result.Errors.Should().Contain($"User with ID {userId} not found.");
    }

    [Fact]
    public async Task AssignRoleToUserAsync_PlatformAdminByNonPlatformAdmin_ReturnsForbidden()
    {
        // Arrange
        SignInAs(CALLER_ID);
        _currentUserAuthorizationService
            .IsPlatformAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));

        // Act
        var result = await _service.AssignRoleToUserAsync(
            1,
            SystemRole.PlatformAdmin.Name,
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        await _userManager.DidNotReceiveWithAnyArgs().FindByIdAsync(default!);
        AssertPlatformAdminMutationWarningLogged();
    }

    [Fact]
    public async Task AssignRoleToUserAsync_WithoutSignedInUser_ReturnsForbidden()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        // Act
        var result = await _service.AssignRoleToUserAsync(1, "Creator", CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        await _userManager.DidNotReceiveWithAnyArgs().AddToRoleAsync(default!, default!);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_SignedInCallerWithNonNumericId_GetsTheCeilingMessage()
    {
        // Arrange
        SignInAs(CALLER_ID);
        _currentUserAuthorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(AuthorizationData.ForAuthenticatedUser("external-subject", 0, [], [])));

        // Act
        var result = await _service.AssignRoleToUserAsync(1, "Creator", CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Errors.Should().ContainSingle().Which.Should().Be("You can only grant access you hold yourself.");
    }

    [Fact]
    public async Task AssignRoleToUserAsync_SignedInCallerWhoseAccessCannotBeRead_GetsTheCeilingMessage()
    {
        // Arrange
        SignInAs(CALLER_ID);
        _currentUserAuthorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result<AuthorizationData>.Error("Failed to retrieve user role information"));

        // Act
        var result = await _service.AssignRoleToUserAsync(1, "Creator", CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Errors.Should().ContainSingle().Which.Should().Be("You can only grant access you hold yourself.");
    }

    [Fact]
    public async Task AssignRoleToUserAsync_OwnRolesByNonAdmin_ReturnsForbidden()
    {
        // Arrange
        SignInAs(CALLER_ID);

        // Act
        var result = await _service.AssignRoleToUserAsync(CALLER_ID, "Creator", CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        await _userManager.DidNotReceiveWithAnyArgs().FindByIdAsync(default!);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_UserInAnotherTenant_ReturnsNotFound()
    {
        // Arrange
        SignInAs(CALLER_ID, SystemRole.Admin.Name);
        _tenantContext.TenantId.Returns(10);
        var user = new AppUser { Id = 1, TenantId = 20 };
        _userManager.FindByIdAsync("1").Returns(user);

        // Act
        var result = await _service.AssignRoleToUserAsync(1, "Creator", CancellationToken.None);

        // Assert
        result.IsNotFound().Should().BeTrue();
        await _userManager.DidNotReceiveWithAnyArgs().AddToRoleAsync(default!, default!);
    }

    #endregion

    #region RemoveRoleFromUserAsync Tests

    [Fact]
    public async Task RemoveRoleFromUserAsync_BlankRoleName_ReturnsInvalidResult()
    {
        // Act
        var result = await _service.RemoveRoleFromUserAsync(1, "", CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Role name is required.");
        await _userManager.DidNotReceiveWithAnyArgs().FindByIdAsync(default!);
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_UserNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        SignInAs(CALLER_ID, SystemRole.Admin.Name);
        var userId = 999L;
        var roleName = "Admin";
        _userManager.FindByIdAsync(userId.ToString()).Returns((AppUser?)null);

        // Act
        var result = await _service.RemoveRoleFromUserAsync(userId, roleName, CancellationToken.None);

        // Assert
        result.IsNotFound().Should().BeTrue();
        result.Errors.Should().Contain($"User with ID {userId} not found.");
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_PlatformAdminByNonPlatformAdmin_ReturnsForbidden()
    {
        // Arrange
        SignInAs(CALLER_ID);
        _currentUserAuthorizationService
            .IsPlatformAdminAsync(Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));

        // Act
        var result = await _service.RemoveRoleFromUserAsync(
            1,
            SystemRole.PlatformAdmin.Name,
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        await _userManager.DidNotReceiveWithAnyArgs().FindByIdAsync(default!);
        AssertPlatformAdminMutationWarningLogged();
    }

    #endregion

    #region ReplaceRolesForUserAsync Tests

    [Fact]
    public async Task ReplaceRolesForUserAsync_PlatformAdminUserByNonPlatformAdmin_ReturnsForbidden()
    {
        // Arrange
        const long tenantId = 10;
        const long userId = 1;
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = new AppUser { Id = userId, TenantId = tenantId };

        _tenantContext.TenantId.Returns(tenantId);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns([SystemRole.PlatformAdmin.Name]);
        _currentUserAuthorizationService
            .IsPlatformAdminAsync(cancellationToken)
            .Returns(Result<bool>.Success(false));

        // Act
        var result = await _service.ReplaceRolesForUserAsync(
            userId,
            [SystemRole.Admin.Name],
            cancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Forbidden);
        await _currentUserAuthorizationService.Received(1).IsPlatformAdminAsync(cancellationToken);
        AssertPlatformAdminMutationWarningLogged();
    }

    #endregion

    #region GetUserRolesAsync Tests

    [Fact]
    public async Task GetUserRolesAsync_InvalidUserId_ReturnsInvalidResult()
    {
        // Act
        var result = await _service.GetUserRolesAsync(0, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("User ID must be greater than zero.");
        await _userManager.DidNotReceiveWithAnyArgs().FindByIdAsync(default!);
    }

    [Fact]
    public async Task GetUserRolesAsync_UserNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var userId = 999L;
        _userManager.FindByIdAsync(userId.ToString()).Returns((AppUser?)null);

        // Act
        var result = await _service.GetUserRolesAsync(userId, CancellationToken.None);

        // Assert
        result.IsNotFound().Should().BeTrue();
        result.Errors.Should().Contain($"User with ID {userId} not found.");
    }

    [Fact]
    public async Task GetUserRolesAsync_ValidUserId_ReturnsSuccessResultWithRoles()
    {
        // Arrange
        var userId = 1L;
        var user = new AppUser { Id = (int)userId };
        var roles = new List<string> { "Admin", "Panelist" };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(roles);

        // Act
        var result = await _service.GetUserRolesAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain("Admin");
        result.Value.Should().Contain("Panelist");

        await _userManager.Received(1).GetRolesAsync(user);
    }

    [Fact]
    public async Task GetUserRolesAsync_ValidUserIdNoRoles_ReturnsSuccessResultWithEmptyList()
    {
        // Arrange
        var userId = 1L;
        var user = new AppUser { Id = (int)userId };
        var roles = new List<string>();

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(roles);

        // Act
        var result = await _service.GetUserRolesAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    #endregion

    private void AssertPlatformAdminMutationWarningLogged()
    {
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("Blocked PlatformAdmin role mutation")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    #region Role Definition Tests

    [Fact]
    public async Task CreateRoleAsync_BlankRoleName_ReturnsInvalidResult()
    {
        // Act
        var result = await _service.CreateRoleAsync(" ", null, ["Permission"], CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Role name is required.");
    }

    [Fact]
    public async Task CreateRoleAsync_EmptyPermissionList_ReturnsInvalidResult()
    {
        // Act
        var result = await _service.CreateRoleAsync("Custom", null, [], CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("At least one permission is required.");
    }

    [Fact]
    public async Task UpdateRoleAsync_BlankPermissionName_ReturnsInvalidResult()
    {
        // Act
        var result = await _service.UpdateRoleAsync("Custom", null, [" "], CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Permission names cannot be empty.");
    }

    [Fact]
    public async Task DeleteRoleAsync_BlankRoleName_ReturnsInvalidResult()
    {
        // Act
        var result = await _service.DeleteRoleAsync("", CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Role name is required.");
    }

    #endregion

    private void SignInAs(long userId, params string[] roles)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test");
        _httpContextAccessor.HttpContext.Returns(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });
        _currentUserAuthorizationService
            .GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(AuthorizationData.ForAuthenticatedUser(userId.ToString(), 0, roles, [])));
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using FluentAssertions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Services;
using Endatix.Infrastructure.Identity.Repositories;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Tests.Identity.Services;

public class RoleManagementServiceTests
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppIdentityDbContext _identityDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRolesRepository _rolesRepository;
    private readonly RoleManagementService _service;

    public RoleManagementServiceTests()
    {
        _userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(),
            null, null, null, null, null, null, null, null);

        var dbOptions = new DbContextOptionsBuilder<AppIdentityDbContext>()
            .Options;
        var valueGeneratorFactory = Substitute.For<EfCoreValueGeneratorFactory>(Substitute.For<IIdGenerator<long>>());
        var idGenerator = Substitute.For<IIdGenerator<long>>();

        _identityDbContext = Substitute.For<AppIdentityDbContext>(dbOptions, valueGeneratorFactory, idGenerator);

        _tenantContext = Substitute.For<ITenantContext>();

        _rolesRepository = Substitute.For<IRolesRepository>();

        _service = new RoleManagementService(_userManager, _identityDbContext, _tenantContext, _rolesRepository);
    }

    #region AssignRoleToUserAsync Tests

    [Fact]
    public async Task AssignRoleToUserAsync_UserNotFound_ReturnsNotFoundResult()
    {
        // Arrange
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
    public async Task AssignRoleToUserAsync_UserAlreadyHasRole_ReturnsInvalidResult()
    {
        // Arrange
        var userId = 1L;
        var roleName = "Admin";
        var user = new AppUser { Id = (int)userId };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.IsInRoleAsync(user, roleName).Returns(true);

        // Act
        var result = await _service.AssignRoleToUserAsync(userId, roleName, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be($"User already has role '{roleName}'.");
    }

    [Fact]
    public async Task AssignRoleToUserAsync_AddToRoleFails_ReturnsErrorResult()
    {
        // Arrange
        var userId = 1L;
        var roleName = "Admin";
        var user = new AppUser { Id = (int)userId };
        var identityResult = IdentityResult.Failed(new IdentityError { Description = "Role assignment failed" });

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.IsInRoleAsync(user, roleName).Returns(false);
        _userManager.AddToRoleAsync(user, roleName).Returns(identityResult);

        // Act
        var result = await _service.AssignRoleToUserAsync(userId, roleName, CancellationToken.None);

        // Assert
        result.IsError().Should().BeTrue();
        result.Errors.Should().Contain("Role assignment failed");
    }

    [Fact]
    public async Task AssignRoleToUserAsync_ValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var userId = 1L;
        var roleName = "Admin";
        var user = new AppUser { Id = (int)userId };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.IsInRoleAsync(user, roleName).Returns(false);
        _userManager.AddToRoleAsync(user, roleName).Returns(IdentityResult.Success);

        // Act
        var result = await _service.AssignRoleToUserAsync(userId, roleName, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userManager.Received(1).AddToRoleAsync(user, roleName);
    }

    #endregion

    #region RemoveRoleFromUserAsync Tests

    [Fact]
    public async Task RemoveRoleFromUserAsync_UserNotFound_ReturnsNotFoundResult()
    {
        // Arrange
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
    public async Task RemoveRoleFromUserAsync_UserDoesNotHaveRole_ReturnsInvalidResult()
    {
        // Arrange
        var userId = 1L;
        var roleName = "Admin";
        var user = new AppUser { Id = (int)userId };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.IsInRoleAsync(user, roleName).Returns(false);

        // Act
        var result = await _service.RemoveRoleFromUserAsync(userId, roleName, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be($"User does not have role '{roleName}'.");
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_RemoveFromRoleFails_ReturnsErrorResult()
    {
        // Arrange
        var userId = 1L;
        var roleName = "Admin";
        var user = new AppUser { Id = (int)userId };
        var identityResult = IdentityResult.Failed(new IdentityError { Description = "Role removal failed" });

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.IsInRoleAsync(user, roleName).Returns(true);
        _userManager.RemoveFromRoleAsync(user, roleName).Returns(identityResult);

        // Act
        var result = await _service.RemoveRoleFromUserAsync(userId, roleName, CancellationToken.None);

        // Assert
        result.IsError().Should().BeTrue();
        result.Errors.Should().Contain("Role removal failed");
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_ValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var userId = 1L;
        var roleName = "Admin";
        var user = new AppUser { Id = (int)userId };
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.IsInRoleAsync(user, roleName).Returns(true);
        _userManager.RemoveFromRoleAsync(user, roleName).Returns(IdentityResult.Success);

        // Act
        var result = await _service.RemoveRoleFromUserAsync(userId, roleName, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userManager.Received(1).RemoveFromRoleAsync(user, roleName);
    }

    #endregion

    #region GetUserRolesAsync Tests

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
}

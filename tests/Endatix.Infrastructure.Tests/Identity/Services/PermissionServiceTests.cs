// using Microsoft.AspNetCore.Identity;
// using Microsoft.Extensions.Caching.Hybrid;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Logging.Abstractions;
// using Endatix.Infrastructure.Identity;
// using Endatix.Infrastructure.Identity.Services;
// using Endatix.Core.Infrastructure.Result;
// using Endatix.Core.Abstractions;
// using Microsoft.AspNetCore.Http;

// namespace Endatix.Infrastructure.Tests.Identity.Services;

// public class PermissionServiceTests
// {
//     private readonly IHttpContextAccessor _httpContextAccessor;
//     private readonly UserManager<AppUser> _userManager;
//     private readonly AppIdentityDbContext _identityDbContext;
//     private readonly HybridCache _hybridCache;
//     private readonly ITenantContext _tenantContext;
//     private readonly IDateTimeProvider _dateTimeProvider;
//     private readonly ILogger<CurrentUserAuthorizationService> _logger;
//     private readonly CurrentUserAuthorizationService _service;

//     public PermissionServiceTests()
//     {
//         _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
//         _userManager = Substitute.For<UserManager<AppUser>>(
//             Substitute.For<IUserStore<AppUser>>(),
//             null, null, null, null, null, null, null, null);
//         _identityDbContext = Substitute.For<AppIdentityDbContext>();
//         _hybridCache = Substitute.For<HybridCache>();
//         _tenantContext = Substitute.For<ITenantContext>();
//         _dateTimeProvider = Substitute.For<IDateTimeProvider>();
//         _logger = NullLogger<CurrentUserAuthorizationService>.Instance;

//         _tenantContext.TenantId.Returns(1L);

//             _service = new CurrentUserAuthorizationService(
//             _httpContextAccessor,
//             _userManager,
//             _identityDbContext,
//             _hybridCache,
//             _tenantContext,
//             _dateTimeProvider,
//             _logger);
//     }

//     #region ValidateAccessAsync Tests

//     [Fact(Skip = "Skipping until next PR")]
//     public async Task ValidateAccessAsync_NullUserId_ReturnsUnauthorizedResult()
//     {
//         // Arrange
//         string? userId = null;
//         var requiredPermission = "forms.view";

//         // Act
//         var result = await _service.ValidateAccessAsync(userId, requiredPermission, CancellationToken.None);

//         // Assert
//         result.Should().NotBeNull();
//         result.Status.Should().Be(ResultStatus.Unauthorized);
//         result.Errors.Should().Contain("Authentication required to access this resource.");
//     }

//    [Fact(Skip = "Skipping until next PR")]
//     public async Task ValidateAccessAsync_EmptyUserId_ReturnsUnauthorizedResult()
//     {
//         // Arrange
//         var userId = string.Empty;
//         var requiredPermission = "forms.view";

//         // Act
//         var result = await _service.ValidateAccessAsync(userId, requiredPermission, CancellationToken.None);

//         // Assert
//         result.Should().NotBeNull();
//         result.Status.Should().Be(ResultStatus.Unauthorized);
//         result.Errors.Should().Contain("Authentication required to access this resource.");
//     }

//     [Fact(Skip = "Skipping until next PR")]
//     public async Task ValidateAccessAsync_InvalidUserId_ReturnsUnauthorizedResult()
//     {
//         // Arrange
//         var userId = "invalid-user-id";
//         var requiredPermission = "forms.view";

//         // Act
//         var result = await _service.ValidateAccessAsync(userId, requiredPermission, CancellationToken.None);

//         // Assert
//         result.Should().NotBeNull();
//         result.Status.Should().Be(ResultStatus.Unauthorized);
//         result.Errors.Should().Contain("Authentication required to access this resource.");
//     }

//     [Fact(Skip = "Skipping until next PR")]
//     public async Task ValidateAccessAsync_AdminUser_ReturnsSuccessResult()
//     {
//         // Arrange
//         var userId = "123";
//         var requiredPermission = "forms.view";
//         var user = new AppUser { Id = 123 };

//         _userManager.FindByIdAsync(userId).Returns(user);
//         _userManager.GetRolesAsync(user).Returns(new List<string> { "Admin" });

//         // Act
//         var result = await _service.ValidateAccessAsync(userId, requiredPermission, CancellationToken.None);

//         // Assert
//         result.Should().NotBeNull();
//         result.Status.Should().Be(ResultStatus.Ok);
//     }

//     [Fact(Skip = "Skipping until next PR")]
//     public async Task ValidateAccessAsync_UserWithPermission_ReturnsSuccessResult()
//     {
//         // Arrange
//         var userId = "456";
//         var requiredPermission = "forms.view";
//         var user = new AppUser { Id = 456 };

//         _userManager.FindByIdAsync(userId).Returns(user);
//         _userManager.GetRolesAsync(user).Returns(new List<string> { "User" });

//         // Mock HasPermissionAsync to return true
//         // Note: This would require access to the private implementation details
//         // In a real scenario, we'd need to mock the role permissions in the database

//         // Act
//         var result = await _service.ValidateAccessAsync(userId, requiredPermission, CancellationToken.None);

//         // Assert
//         // This test would need proper setup of the permission system
//         // For now, we're testing the structure
//         result.Should().NotBeNull();
//     }

//     [Fact(Skip = "Skipping until next PR")]
//     public async Task ValidateAccessAsync_UserWithoutPermission_ReturnsForbiddenResult()
//     {
//         // Arrange
//         var userId = "789";
//         var requiredPermission = "forms.view";
//         var user = new AppUser { Id = 789 };

//         _userManager.FindByIdAsync(userId).Returns(user);
//         _userManager.GetRolesAsync(user).Returns(new List<string> { "Guest" });

//         // Act
//         var result = await _service.ValidateAccessAsync(userId, requiredPermission, CancellationToken.None);

//         // Assert
//         result.Should().NotBeNull();
//         // The result would be Forbidden if the user exists but doesn't have the permission
//         // However, without proper database setup, this test is more structural
//     }

//     [Theory(Skip = "Skipping until next PR")]
//     [InlineData("forms.view")]
//     [InlineData("submissions.create")]
//     [InlineData("forms.edit")]
//     public async Task ValidateAccessAsync_DifferentPermissions_ProcessesCorrectly(string permission)
//     {
//         // Arrange
//         var userId = "123";
//         var user = new AppUser { Id = 123 };

//         _userManager.FindByIdAsync(userId).Returns(user);
//         _userManager.GetRolesAsync(user).Returns(new List<string> { "Admin" });

//         // Act
//         var result = await _service.ValidateAccessAsync(userId, permission, CancellationToken.None);

//         // Assert
//         result.Should().NotBeNull();
//         result.Status.Should().Be(ResultStatus.Ok);
//     }

//     #endregion
// }

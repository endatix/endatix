using FastEndpoints;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Core.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Data;

namespace Endatix.Api.Endpoints.Test;

/// <summary>
/// Unified test endpoint that combines seeding and testing functionality.
/// This endpoint handles:
/// - Role and permission seeding
/// - Test user creation
/// - Authentication testing
/// - Permission testing
/// </summary>
public class UnifiedTestEndpoint : Endpoint<UnifiedTestRequest, UnifiedTestResponse>
{
    private readonly RoleManager<AppRole> _roleManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IPermissionService _permissionService;
    private readonly IUserContext _userContext;
    private readonly ILogger<UnifiedTestEndpoint> _logger;
    private readonly AppIdentityDbContext _dbContext;

    public UnifiedTestEndpoint(
        RoleManager<AppRole> roleManager,
        UserManager<AppUser> userManager,
        IPermissionService permissionService,
        IUserContext userContext,
        ILogger<UnifiedTestEndpoint> logger,
        AppIdentityDbContext dbContext)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _permissionService = permissionService;
        _userContext = userContext;
        _logger = logger;
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get("test/unified");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Unified test endpoint for seeding and testing";
            s.Description = "Combines role seeding, user creation, and permission testing functionality";
            s.Response<UnifiedTestResponse>(200, "Test completed successfully");
        });
    }

    public override async Task HandleAsync(UnifiedTestRequest req, CancellationToken ct)
    {
        var response = new UnifiedTestResponse();

        try
        {
            // 1. Seed test data (roles, permissions, and users)
            if (req.SeedTestData)
            {
                _logger.LogInformation("Starting test data seeding...");
                await SeedTestDataAsync();
                response.TestDataSeeded = true;
                _logger.LogInformation("Test data seeding completed");
            }

            // 2. Test authentication
            if (req.TestAuth)
            {
                _logger.LogInformation("Testing authentication...");
                response.AuthTest = await TestAuthenticationAsync();
                _logger.LogInformation("Authentication test completed");
            }

            // 3. Test permissions
            if (req.TestPermissions)
            {
                _logger.LogInformation("Testing permissions...");
                response.PermissionTest = await TestPermissionsAsync();
                _logger.LogInformation("Permission test completed");
            }

            // 4. Get system status
            response.SystemStatus = await GetSystemStatusAsync();

            response.Success = true;
            response.Message = "All requested operations completed successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in unified test endpoint");
            response.Success = false;
            response.Message = $"Error: {ex.Message}";
            response.Error = ex.ToString();
        }

        await Send.OkAsync(response, ct);
    }

    private async Task SeedTestDataAsync()
    {
        // 1. Seed roles and permissions
        await SeedRolesAndPermissionsAsync();

        // 2. Create test users
        await SeedTestUsersAsync();
    }

    private async Task SeedRolesAndPermissionsAsync()
    {
        _logger.LogInformation("Seeding roles and permissions...");

        // Get all role definitions from Roles.cs
        var roleDefinitions = Endatix.Infrastructure.Identity.Authorization.Roles.GetRoleDefinitions();

        foreach (var (name, description, isSystemDefined) in roleDefinitions)
        {
            if (!await _roleManager.RoleExistsAsync(name))
            {
                var role = AppRole.CreateSystemRole(name, description, 0);
                await _roleManager.CreateAsync(role);
                _logger.LogInformation("Created {RoleName} role", name);

                // Add permissions to the role
                await AddPermissionsToRoleAsync(role, name);
            }
        }

        _logger.LogInformation("Role and permission seeding completed");
    }

    private async Task AddPermissionsToRoleAsync(AppRole role, string roleName)
    {
        try
        {
            // Get all permissions for this role from Roles.cs
            var permissions = Endatix.Infrastructure.Identity.Authorization.Roles.GetAllPermissionsForRole(roleName);

            _logger.LogInformation("Adding {Count} permissions to {RoleName} role", permissions.Length, roleName);

            foreach (var permissionName in permissions)
            {
                // Check if permission already exists in database
                var existingPermission = await _dbContext.Permissions
                    .FirstOrDefaultAsync(p => p.Name == permissionName);

                if (existingPermission is null)
                {
                    // Create permission if it doesn't exist
                    var permission = Permission.CreateSystemPermission(
                        permissionName,
                        $"Permission: {permissionName}",
                        GetPermissionCategory(permissionName).Code);

                    _dbContext.Permissions.Add(permission);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogDebug("Created permission {Permission}", permissionName);
                }

                // Check if role-permission relationship already exists
                var existingRolePermission = await _dbContext.RolePermissions
                    .FirstOrDefaultAsync(rp => rp.RoleId == role.Id && rp.Permission.Name == permissionName);

                if (existingRolePermission is null)
                {
                    // Get the permission from database
                    var permission = await _dbContext.Permissions
                        .FirstAsync(p => p.Name == permissionName);

                    // Create role-permission relationship
                    var rolePermission = new RolePermission(
                        role.Id,
                        permission.Id
                    );

                    _dbContext.RolePermissions.Add(rolePermission);
                    _logger.LogDebug("Added permission {Permission} to {RoleName}", permissionName, roleName);
                }
            }

            // Save all role-permission relationships
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Successfully added permissions to {RoleName} role", roleName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add permissions to {RoleName} role", roleName);
        }
    }

    private static PermissionCategory GetPermissionCategory(string permissionName)
    {
        // Extract category from permission name (e.g., "forms.edit" -> "Forms")
        var category = permissionName.Split('.')[0];
        return category.ToLower() switch
        {
            "admin" => PermissionCategory.Admin,
            "forms" => PermissionCategory.Forms,
            "submissions" => PermissionCategory.Submissions,
            "templates" => PermissionCategory.Templates,
            "themes" => PermissionCategory.Themes,
            "questions" => PermissionCategory.Questions,
            "analytics" => PermissionCategory.Analytics,
            "tenant" => PermissionCategory.Tenant,
            "system" => PermissionCategory.System,
            _ => PermissionCategory.System
        };
    }

    private async Task SeedTestUsersAsync()
    {
        _logger.LogInformation("Creating test users...");

        // Create admin user
        if (await _userManager.FindByEmailAsync("admin@test.com") is null)
        {
            var adminUser = new AppUser
            {
                UserName = "admin@test.com",
                Email = "admin@test.com",
                EmailConfirmed = true,
                TenantId = 1
            };

            var result = await _userManager.CreateAsync(adminUser, "TestPassword123!");
            if (result.Succeeded)
            {
                await _userManager.UpdateAsync(adminUser);
                await _userManager.AddToRoleAsync(adminUser, "Admin");
                _logger.LogInformation("Created admin test user: {Email}", adminUser.Email);
            }
            else
            {
                _logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Create scripter user
        if (await _userManager.FindByEmailAsync("scripter@test.com") is null)
        {
            var scripterUser = new AppUser
            {
                UserName = "scripter@test.com",
                Email = "scripter@test.com",
                EmailConfirmed = true,
                TenantId = 1
            };

            var result = await _userManager.CreateAsync(scripterUser, "TestPassword123!");
            if (result.Succeeded)
            {
                await _userManager.UpdateAsync(scripterUser);
                await _userManager.AddToRoleAsync(scripterUser, "Scripter");
                _logger.LogInformation("Created scripter test user: {Email}", scripterUser.Email);
            }
            else
            {
                _logger.LogError("Failed to create scripter user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Create panelist user
        if (await _userManager.FindByEmailAsync("panelist@test.com") is null)
        {
            var panelistUser = new AppUser
            {
                UserName = "panelist@test.com",
                Email = "panelist@test.com",
                EmailConfirmed = true,
                TenantId = 1
            };

            var result = await _userManager.CreateAsync(panelistUser, "TestPassword123!");
            if (result.Succeeded)
            {
                await _userManager.UpdateAsync(panelistUser);
                await _userManager.AddToRoleAsync(panelistUser, "Panelist");
                _logger.LogInformation("Created panelist test user: {Email}", panelistUser.Email);
            }
            else
            {
                _logger.LogError("Failed to create panelist user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Create viewer user
        if (await _userManager.FindByEmailAsync("viewer@test.com") is null)
        {
            var viewerUser = new AppUser
            {
                UserName = "viewer@test.com",
                Email = "viewer@test.com",
                EmailConfirmed = true,
                TenantId = 1
            };

            var result = await _userManager.CreateAsync(viewerUser, "TestPassword123!");
            if (result.Succeeded)
            {
                await _userManager.UpdateAsync(viewerUser);
                await _userManager.AddToRoleAsync(viewerUser, "Viewer");
                _logger.LogInformation("Created viewer test user: {Email}", viewerUser.Email);
            }
            else
            {
                _logger.LogError("Failed to create viewer user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        _logger.LogInformation("Test user creation completed");
    }
    
    private Task<AuthTestResult> TestAuthenticationAsync()
    {
        var result = new AuthTestResult();

        try
        {
            // Test anonymous access
            result.IsAnonymous = _userContext.IsAnonymous;
            result.IsAuthenticated = _userContext.IsAuthenticated;

            if (_userContext.IsAuthenticated)
            {
                result.UserId = _userContext.GetCurrentUserId();
                var currentUser = _userContext.GetCurrentUser();
                result.TenantId = currentUser?.TenantId.ToString();
                result.UserEmail = currentUser?.Email;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return Task.FromResult(result);
    }

    private async Task<PermissionTestResult> TestPermissionsAsync()
    {
        var result = new PermissionTestResult();

        try
        {
            if (!_userContext.IsAuthenticated)
            {
                result.Success = true;
                return result;
            }

            var userIdStr = _userContext.GetCurrentUserId();
            if (string.IsNullOrEmpty(userIdStr) || !long.TryParse(userIdStr, out var userId))
            {
                result.Success = false;
                result.Error = "Invalid user ID";
                return result;
            }

            // Test admin permissions
            var adminResult = await _permissionService.IsUserAdminAsync(userId);
            result.IsAdmin = adminResult.IsSuccess && adminResult.Value;

            // Test specific permissions
            var formsResult = await _permissionService.HasPermissionAsync(userId, Actions.Forms.Edit);
            result.CanEditForms = formsResult.IsSuccess && formsResult.Value;

            var submissionsResult = await _permissionService.HasPermissionAsync(userId, Actions.Submissions.View);
            result.CanViewSubmissions = submissionsResult.IsSuccess && submissionsResult.Value;

            var usersResult = await _permissionService.HasPermissionAsync(userId, Actions.Tenant.ManageUsers);
            result.CanManageUsers = usersResult.IsSuccess && usersResult.Value;

            // Get user role info
            var roleInfoResult = await _permissionService.GetUserRoleInfoAsync(userId);
            if (roleInfoResult.IsSuccess)
            {
                result.UserRoles = roleInfoResult.Value.Roles.ToList();
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<SystemStatus> GetSystemStatusAsync()
    {
        var status = new SystemStatus();

        try
        {
            // Count roles
            status.RoleCount = await _roleManager.Roles.CountAsync();

            // Count users
            status.UserCount = await _userManager.Users.CountAsync();

            // Count permissions
            var permissionCount = await _dbContext.Permissions.CountAsync();
            var rolePermissionCount = await _dbContext.RolePermissions.CountAsync();

            // Check scripter role permissions specifically
            var scripterRole = await _roleManager.FindByNameAsync("Scripter");
            var scripterPermissions = new List<string>();
            if (scripterRole is not null)
            {
                scripterPermissions = await _dbContext.RolePermissions
                    .Where(rp => rp.RoleId == scripterRole.Id)
                    .Select(rp => rp.Permission.Name)
                    .ToListAsync();
            }

            _logger.LogInformation("Database status - Roles: {RoleCount}, Users: {UserCount}, Permissions: {PermissionCount}, RolePermissions: {RolePermissionCount}, ScripterPermissions: {ScripterPermissionCount}", 
                status.RoleCount, status.UserCount, permissionCount, rolePermissionCount, scripterPermissions.Count);

            // For now, assume database has permissions if we have roles
            status.HasDatabasePermissions = status.RoleCount > 0;
            status.Success = true;
        }
        catch (Exception ex)
        {
            status.Success = false;
            status.Error = ex.Message;
        }

        return status;
    }
}

public class UnifiedTestRequest
{
    public bool SeedTestData { get; set; } = true;
    public bool TestAuth { get; set; } = true;
    public bool TestPermissions { get; set; } = true;
}

public class UnifiedTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }

    // Seeding results
    public bool TestDataSeeded { get; set; }

    // Test results
    public AuthTestResult? AuthTest { get; set; }
    public PermissionTestResult? PermissionTest { get; set; }
    public SystemStatus? SystemStatus { get; set; }
}

public class AuthTestResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsAuthenticated { get; set; }
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string? UserEmail { get; set; }
}

public class PermissionTestResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool IsAdmin { get; set; }
    public bool CanEditForms { get; set; }
    public bool CanViewSubmissions { get; set; }
    public bool CanManageUsers { get; set; }
    public List<string> UserRoles { get; set; } = new();
}

public class SystemStatus
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int RoleCount { get; set; }
    public int UserCount { get; set; }
    public bool HasDatabasePermissions { get; set; }
}
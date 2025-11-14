using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Seed;

public class RolesSeeder
{
    private readonly ILogger _logger;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly AppIdentityDbContext _dbContext;

    public RolesSeeder(ILogger logger, UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, AppIdentityDbContext dbContext)
    {
        _logger = logger;
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
    }

    public async Task SeedSystemRolesAsync()
    {
        _logger.LogInformation("Seeding system roles and default permissions...");

        var systemRoles = SystemRole
                     .AllSystemRoles
                     .Where(r => r.IsPersisted)
                     .ToList();

        await AddSystemPermissionsAsync();
        await AddSystemRolesAsync(systemRoles);
        await AddPermissionsToRoleAsync(systemRoles);
        await AssignAdminRoleToExistingUserAsync();

        _logger.LogInformation("Role and permission seeding completed");
    }

    /// <summary>
    /// Adds system permissions to the database
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task AddSystemPermissionsAsync()
    {
        try
        {
            var systemPermissions = PermissionBuilder.GetAllPermissions();

            foreach (var permission in systemPermissions)
            {
                if (!await _dbContext.Permissions.AnyAsync(p => p.Name == permission))
                {
                    var newPermission = Permission.CreateSystemPermission(
                    permission,
                    $"Permission: {permission}",
                    PermissionBuilder.GetPermissionCategory(permission).Code);

                    _dbContext.Permissions.Add(newPermission);
                    _logger.LogDebug("Created permission {Permission}", permission);
                    await _dbContext.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add system permissions");
        }
    }


    /// <summary>
    /// Adds system roles to the database
    /// </summary>
    /// <param name="systemRoles">The system role definitions to add</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task AddSystemRolesAsync(IEnumerable<SystemRole> systemRoles)
    {
        try
        {
            foreach (var role in systemRoles)
            {
                if (!await _roleManager.RoleExistsAsync(role.Name))
                {
                    var roleToAdd = AppRole.CreateSystemRole(role.Name, role.Description, AuthConstants.DEFAULT_TENANT_ID);
                    await _roleManager.CreateAsync(roleToAdd);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add system roles");
        }
    }


    /// <summary>
    /// Adds permissions to the roles
    /// </summary>
    /// <param name="systemRoles">The system role definitions to add permissions to</param>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task AddPermissionsToRoleAsync(IEnumerable<SystemRole> systemRoles)
    {
        try
        {
            foreach (var systemRole in systemRoles)
            {
                var role = await _roleManager.FindByNameAsync(systemRole.Name);
                if (role == null)
                {
                    throw new Exception($"Role {systemRole.Name} not found. Aborting data seeding.");
                }

                var permissionsToAdd = _dbContext.Permissions.Where(p => systemRole.Permissions.Contains(p.Name)).ToList();
                _dbContext.RolePermissions.AddRange(permissionsToAdd.Select(p => new RolePermission(role.Id, p.Id)));

                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add permissions to role");
        }
    }

    /// <summary>
    /// Assigns the platform admin role to existing validated users
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task AssignAdminRoleToExistingUserAsync()
    {
        try
        {
            var validatedUsers = await _dbContext
           .Users
           .Where(u => u.TenantId == AuthConstants.DEFAULT_ADMIN_TENANT_ID && u.EmailConfirmed)
           .ToListAsync();

            if (!validatedUsers.Any())
            {
                throw new Exception("No validated users found. Aborting admin role assignment.");
            }

            foreach (var user in validatedUsers)
            {
                await _userManager.AddToRoleAsync(user, SystemRole.PlatformAdmin.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign admin role to existing users");
        }
    }
}
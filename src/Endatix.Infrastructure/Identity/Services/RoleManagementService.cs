using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Services;

/// <summary>
/// Service for managing user roles.
/// </summary>
public sealed class RoleManagementService : IRoleManagementService
{
    private const string PlatformAdminRoleMutationForbiddenMessage = "Only platform administrators can assign or remove the PlatformAdmin role.";
    private const string PlatformAdminUserMutationForbiddenMessage = "Only platform administrators can modify users with the PlatformAdmin role.";
    private const string ExternalUserRoleMutationForbiddenMessage = "External users receive roles from their identity provider and cannot be edited locally.";

    private static readonly IReadOnlyDictionary<string, SystemRole> _persistedSystemRolesByName =
        SystemRole.AllSystemRoles.ToDictionary(role => role.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions _externalRolesJsonSerializerOptions = new();

    private readonly UserManager<AppUser> _userManager;
    private readonly AppIdentityDbContext _identityDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRolesRepository _rolesRepository;
    private readonly IIdGenerator<long> _idGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserAuthorizationService _currentUserAuthorizationService;
    private readonly ILogger<RoleManagementService> _logger;

    public RoleManagementService(
        UserManager<AppUser> userManager,
        AppIdentityDbContext identityDbContext,
        ITenantContext tenantContext,
        IRolesRepository rolesRepository,
        IIdGenerator<long> idGenerator,
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserAuthorizationService currentUserAuthorizationService,
        ILogger<RoleManagementService> logger)
    {
        _userManager = userManager;
        _identityDbContext = identityDbContext;
        _tenantContext = tenantContext;
        _rolesRepository = rolesRepository;
        _idGenerator = idGenerator;
        _httpContextAccessor = httpContextAccessor;
        _currentUserAuthorizationService = currentUserAuthorizationService;
        _logger = logger;
    }
    /// <inheritdoc/>
    public async Task<Result> AssignRoleToUserAsync(long userId, string roleName, CancellationToken cancellationToken = default)
    {
        var inputGuard = ValidateRoleMutationInput(userId, roleName);
        if (!inputGuard.IsSuccess)
        {
            return inputGuard;
        }

        var trimmedRoleName = roleName.Trim();
        var platformAdminRoleGuard = await EnsureCurrentUserCanMutatePlatformAdminRoleAsync(trimmedRoleName, cancellationToken);
        if (!platformAdminRoleGuard.IsSuccess)
        {
            return platformAdminRoleGuard;
        }

        var user = await FindUserByIdAsync(userId);
        if (user is null)
        {
            return UserNotFound(userId);
        }

        var externalUserGuard = EnsureExternalUserRolesAreReadOnly(user);
        if (!externalUserGuard.IsSuccess)
        {
            return externalUserGuard;
        }

        var isInRole = await _userManager.IsInRoleAsync(user, trimmedRoleName);
        if (isInRole)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"User already has role '{trimmedRoleName}'."
            });
        }

        var result = await _userManager.AddToRoleAsync(user, trimmedRoleName);
        if (!result.Succeeded)
        {
            var errorMessages = result.Errors.Select(e => e.Description);
            return Result.Error(new ErrorList(errorMessages));
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> RemoveRoleFromUserAsync(long userId, string roleName, CancellationToken cancellationToken = default)
    {
        var inputGuard = ValidateRoleMutationInput(userId, roleName);
        if (!inputGuard.IsSuccess)
        {
            return inputGuard;
        }

        var trimmedRoleName = roleName.Trim();
        var platformAdminRoleGuard = await EnsureCurrentUserCanMutatePlatformAdminRoleAsync(trimmedRoleName, cancellationToken);
        if (!platformAdminRoleGuard.IsSuccess)
        {
            return platformAdminRoleGuard;
        }

        var user = await FindUserByIdAsync(userId);
        if (user is null)
        {
            return UserNotFound(userId);
        }

        var externalUserGuard = EnsureExternalUserRolesAreReadOnly(user);
        if (!externalUserGuard.IsSuccess)
        {
            return externalUserGuard;
        }

        var isInRole = await _userManager.IsInRoleAsync(user, trimmedRoleName);
        if (!isInRole)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"User does not have role '{trimmedRoleName}'."
            });
        }

        var result = await _userManager.RemoveFromRoleAsync(user, trimmedRoleName);
        if (!result.Succeeded)
        {
            var errorMessages = result.Errors.Select(e => e.Description);
            return Result.Error(new ErrorList(errorMessages));
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> ReplaceRolesForUserAsync(long userId, IReadOnlyList<string> roleNames, CancellationToken cancellationToken = default)
    {
        var userIdGuard = ValidateUserId(userId);
        if (!userIdGuard.IsSuccess)
        {
            return userIdGuard;
        }

        var roleNamesGuard = ValidateReplacementRoleNames(roleNames);
        if (!roleNamesGuard.IsSuccess)
        {
            return roleNamesGuard;
        }

        var tenantId = _tenantContext.TenantId;
        var user = await FindUserByIdAsync(userId);
        if (user is null || user.TenantId != tenantId)
        {
            return UserNotFound(userId);
        }

        var externalUserGuard = EnsureExternalUserRolesAreReadOnly(user);
        if (!externalUserGuard.IsSuccess)
        {
            return externalUserGuard;
        }

        var platformAdminUserGuard = await EnsureCurrentUserCanModifyPlatformAdminUserAsync(user, cancellationToken);
        if (!platformAdminUserGuard.IsSuccess)
        {
            return platformAdminUserGuard;
        }

        var requestedRoleNames = GetDistinctRoleNames(roleNames);
        var requestedNormalizedRoleNames = requestedRoleNames
            .Select(NormalizeRoleName)
            .ToList();

        var requestedEditableRoles = requestedNormalizedRoleNames.Count == 0
            ? new List<AppRole>()
            : await _identityDbContext.Roles
                .Where(role =>
                    role.IsActive &&
                    requestedNormalizedRoleNames.Contains(role.NormalizedName!) &&
                    (role.TenantId == tenantId ||
                     (role.IsSystemDefined && role.TenantId <= 0 && role.Name != SystemRole.PlatformAdmin.Name)))
                .ToListAsync(cancellationToken);

        var rolesGuard = EnsureAllRolesExist(requestedRoleNames, requestedEditableRoles);
        if (!rolesGuard.IsSuccess)
        {
            return rolesGuard;
        }

        var currentEditableRoleAssignments = await _identityDbContext.UserRoles
            .Join(
                _identityDbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { UserRole = userRole, Role = role })
            .Where(item =>
                item.UserRole.UserId == userId &&
                (item.Role.TenantId == tenantId ||
                 (item.Role.IsSystemDefined && item.Role.TenantId <= 0 && item.Role.Name != SystemRole.PlatformAdmin.Name)))
            .ToListAsync(cancellationToken);

        var requestedEditableRoleIds = requestedEditableRoles
            .Select(role => role.Id)
            .ToHashSet();
        var currentEditableRoleIds = currentEditableRoleAssignments
            .Select(item => item.UserRole.RoleId)
            .ToHashSet();

        var staleEditableRoleAssignments = currentEditableRoleAssignments
            .Where(item => !requestedEditableRoleIds.Contains(item.UserRole.RoleId))
            .Select(item => item.UserRole)
            .ToList();
        var missingEditableRoleIds = requestedEditableRoleIds
            .Where(roleId => !currentEditableRoleIds.Contains(roleId))
            .ToList();

        _identityDbContext.UserRoles.RemoveRange(staleEditableRoleAssignments);
        foreach (var roleId in missingEditableRoleIds)
        {
            _identityDbContext.UserRoles.Add(new IdentityUserRole<long>
            {
                UserId = userId,
                RoleId = roleId
            });
        }

        await _identityDbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<IList<string>>> GetUserRolesAsync(long userId, CancellationToken cancellationToken = default)
    {
        var userIdGuard = ValidateUserId(userId);
        if (!userIdGuard.IsSuccess)
        {
            return userIdGuard.ToErrorResult<IList<string>>();
        }

        var user = await FindUserByIdAsync(userId);
        if (user is null)
        {
            return Result<IList<string>>.NotFound($"User with ID {userId} not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Result<IList<string>>.Success(roles);
    }

    /// <inheritdoc/>
    public async Task<Result<string>> CreateRoleAsync(string name, string? description, List<string> permissionNames, CancellationToken cancellationToken = default)
    {
        var roleNameGuard = ValidateRoleName(name);
        if (!roleNameGuard.IsSuccess)
        {
            return roleNameGuard.ToErrorResult<string>();
        }

        var permissionNamesGuard = ValidatePermissionNames(permissionNames, allowEmpty: false);
        if (!permissionNamesGuard.IsSuccess)
        {
            return permissionNamesGuard.ToErrorResult<string>();
        }

        var tenantId = _tenantContext.TenantId;
        var roleName = name.Trim();
        var normalizedRoleName = NormalizeRoleName(roleName);
        var distinctPermissionNames = GetDistinctPermissionNames(permissionNames);

        if (SystemRole.AllSystemRoleNames.Contains(roleName, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(name),
                ErrorMessage = $"Role '{roleName}' is system-defined and cannot be created as a custom role."
            });
        }

        var roleExists = await _identityDbContext.Roles
            .AnyAsync(role => role.NormalizedName == normalizedRoleName && role.TenantId == tenantId, cancellationToken);
        if (roleExists)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(name),
                ErrorMessage = $"Role '{roleName}' already exists for this tenant."
            });
        }

        var permissions = await _identityDbContext.Permissions
            .AsNoTracking()
            .Where(permission => distinctPermissionNames.Contains(permission.Name))
            .ToListAsync(cancellationToken);

        var permissionsGuard = EnsureAllPermissionsExist(distinctPermissionNames, permissions);
        if (!permissionsGuard.IsSuccess)
        {
            return permissionsGuard.ToErrorResult<string>();
        }

        var role = new AppRole
        {
            Name = roleName,
            NormalizedName = normalizedRoleName,
            Description = description,
            TenantId = tenantId,
            IsSystemDefined = false,
            IsActive = true
        };

        var permissionIds = permissions.Select(p => p.Id).ToList();
        var createdRole = await _rolesRepository.CreateRoleWithPermissionsAsync(role, permissionIds, cancellationToken);

        return Result<string>.Created(createdRole.Id.ToString());
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var roleNameGuard = ValidateRoleName(roleName);
        if (!roleNameGuard.IsSuccess)
        {
            return roleNameGuard;
        }

        var tenantId = _tenantContext.TenantId;
        var trimmedRoleName = roleName.Trim();
        var normalizedRoleName = NormalizeRoleName(trimmedRoleName);

        var role = await _identityDbContext.Roles
            .FirstOrDefaultAsync(role => role.NormalizedName == normalizedRoleName && role.TenantId == tenantId, cancellationToken);

        if (role is null)
        {
            return Result.NotFound($"Role '{trimmedRoleName}' not found for this tenant.");
        }

        if (role.IsSystemDefined)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"Cannot delete system-defined role '{trimmedRoleName}'."
            });
        }

        var isRoleAssignedToUsers = await _identityDbContext.UserRoles
            .AnyAsync(ur => ur.RoleId == role.Id, cancellationToken);

        if (isRoleAssignedToUsers)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"Cannot delete role '{trimmedRoleName}' because it is assigned to one or more users."
            });
        }

        await _rolesRepository.DeleteRoleAsync(role, cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<Paged<RoleListItem>>> ListRolesAsync(int skip, int take, string? roleType, string? search, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;

        var roles = await LoadPersistedRolesWithPermissionsAsync(tenantId, cancellationToken);
        var nativeRoleUserCounts = await LoadNativeRoleUserCountsAsync(tenantId, cancellationToken);
        var externalRoleUserCountsByName =
            await LoadExternalRoleUserCountsByNameAsync(tenantId, cancellationToken);

        var persistedItems = BuildPersistedRoleListItems(
            roles,
            nativeRoleUserCounts,
            externalRoleUserCountsByName);

        var allItems = MergeWithPersistedSystemRolePlaceholders(persistedItems);
        allItems = ApplyRoleListFilters(allItems, roleType, search);

        var totalRecords = allItems.Count;
        var pagedItems = allItems.Skip(skip).Take(take).ToList();

        return Result<Paged<RoleListItem>>.Success(
            Paged<RoleListItem>.FromSkipAndTake(skip, take, totalRecords, pagedItems));
    }

    private async Task<IReadOnlyList<PersistedRoleRow>> LoadPersistedRolesWithPermissionsAsync(
        long tenantId,
        CancellationToken cancellationToken)
    {
        var roleHeaders = await _identityDbContext.Roles
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId || (role.IsSystemDefined && role.TenantId <= 0))
            .Select(role => new RoleHeaderRow(
                role.Id,
                role.Name ?? string.Empty,
                role.Description,
                role.IsSystemDefined,
                role.IsActive))
            .ToListAsync(cancellationToken);

        if (roleHeaders.Count == 0)
        {
            return [];
        }

        var roleIds = roleHeaders.Select(role => role.Id).ToList();
        var permissionNamesByRoleId = await LoadActivePermissionNamesByRoleIdAsync(
            roleIds,
            cancellationToken);

        return roleHeaders
            .Select(role => new PersistedRoleRow(
                role.Id,
                role.Name,
                role.Description,
                role.IsSystemDefined,
                role.IsActive,
                permissionNamesByRoleId.GetValueOrDefault(role.Id, [])))
            .ToList();
    }

    private async Task<Dictionary<long, List<string>>> LoadActivePermissionNamesByRoleIdAsync(
        IReadOnlyCollection<long> roleIds,
        CancellationToken cancellationToken)
    {
        var permissionRows = await _identityDbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => rolePermission.IsActive && roleIds.Contains(rolePermission.RoleId))
            .Join(
                _identityDbContext.Permissions.AsNoTracking(),
                rolePermission => rolePermission.PermissionId,
                permission => permission.Id,
                (rolePermission, permission) => new RolePermissionNameRow(rolePermission.RoleId, permission.Name))
            .ToListAsync(cancellationToken);

        return permissionRows
            .GroupBy(row => row.RoleId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(row => row.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList());
    }

    private async Task<IReadOnlyDictionary<long, int>> LoadNativeRoleUserCountsAsync(
        long tenantId,
        CancellationToken cancellationToken)
    {
        return await (
            from userRole in _identityDbContext.UserRoles.AsNoTracking()
            join user in _identityDbContext.Users.AsNoTracking() on userRole.UserId equals user.Id
            where user.TenantId == tenantId
            group userRole by userRole.RoleId into roleGroup
            select new { RoleId = roleGroup.Key, Count = roleGroup.Count() })
            .ToDictionaryAsync(item => item.RoleId, item => item.Count, cancellationToken);
    }

    private static List<RoleListItem> BuildPersistedRoleListItems(
        IReadOnlyList<PersistedRoleRow> roles,
        IReadOnlyDictionary<long, int> nativeRoleUserCounts,
        IReadOnlyDictionary<string, int> externalRoleUserCountsByName)
    {
        return roles
            .Select(role =>
            {
                _persistedSystemRolesByName.TryGetValue(role.Name, out var systemRole);

                return new RoleListItem
                {
                    Id = role.Id,
                    Name = role.Name,
                    Description = role.Description ?? systemRole?.Description,
                    IsSystemDefined = role.IsSystemDefined,
                    IsActive = role.IsActive,
                    Permissions = role.PermissionNames.Count > 0 || systemRole is null
                        ? role.PermissionNames
                        : systemRole.Permissions
                            .OrderBy(permissionName => permissionName, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                    UsersCount = nativeRoleUserCounts.GetValueOrDefault(role.Id) +
                        externalRoleUserCountsByName.GetValueOrDefault(role.Name)
                };
            })
            .ToList();
    }

    private static List<RoleListItem> MergeWithPersistedSystemRolePlaceholders(IReadOnlyList<RoleListItem> persistedItems)
    {
        var itemsByName = SystemRole.AllSystemRoles
            .Where(role => role.IsPersisted)
            .Select((role, index) => new RoleListItem
            {
                Id = -(index + 1),
                Name = role.Name,
                Description = role.Description,
                IsSystemDefined = true,
                IsActive = true,
                Permissions = role.Permissions
                    .OrderBy(permissionName => permissionName, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToDictionary(role => role.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var persistedItem in persistedItems)
        {
            itemsByName[persistedItem.Name] = persistedItem;
        }

        return itemsByName.Values
            .OrderBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<RoleListItem> ApplyRoleListFilters(
        List<RoleListItem> roles,
        string? roleType,
        string? search)
    {
        IEnumerable<RoleListItem> filteredRoles = roles;

        if (!string.IsNullOrWhiteSpace(roleType))
        {
            filteredRoles = roleType switch
            {
                "system" => filteredRoles.Where(role => role.IsSystemDefined),
                "custom" => filteredRoles.Where(role => !role.IsSystemDefined),
                _ => filteredRoles
            };
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filteredRoles = filteredRoles.Where(role =>
                role.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return filteredRoles.ToList();
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<PermissionListItem>>> ListPermissionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PermissionListItem> permissions = await _identityDbContext.Permissions
            .AsNoTracking()
            .Where(permission => permission.IsActive)
            .OrderBy(permission => permission.Category)
            .ThenBy(permission => permission.Name)
            .Select(permission => new PermissionListItem
            {
                Id = permission.Id,
                Name = permission.Name,
                Description = permission.Description,
                Category = permission.Category,
                IsSystemDefined = permission.IsSystemDefined
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PermissionListItem>>.Success(permissions);
    }

    /// <inheritdoc/>
    public async Task<Result<string>> UpdateRoleAsync(
        string roleName,
        string? description,
        List<string> permissionNames,
        CancellationToken cancellationToken = default)
    {
        var roleNameGuard = ValidateRoleName(roleName);
        if (!roleNameGuard.IsSuccess)
        {
            return roleNameGuard.ToErrorResult<string>();
        }

        var permissionNamesGuard = ValidatePermissionNames(permissionNames, allowEmpty: true);
        if (!permissionNamesGuard.IsSuccess)
        {
            return permissionNamesGuard.ToErrorResult<string>();
        }

        var tenantId = _tenantContext.TenantId;
        var trimmedRoleName = roleName.Trim();
        var normalizedRoleName = NormalizeRoleName(trimmedRoleName);
        var distinctPermissionNames = GetDistinctPermissionNames(permissionNames);

        var role = await _identityDbContext.Roles
            .FirstOrDefaultAsync(role => role.NormalizedName == normalizedRoleName && role.TenantId == tenantId, cancellationToken);

        if (role is null)
        {
            return Result.NotFound($"Role '{trimmedRoleName}' not found for this tenant.");
        }

        if (role.IsSystemDefined)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"Cannot update system-defined role '{trimmedRoleName}'."
            });
        }

        var permissions = distinctPermissionNames.Count == 0
            ? []
            : await _identityDbContext.Permissions
                .Where(permission => distinctPermissionNames.Contains(permission.Name))
                .ToListAsync(cancellationToken);

        var permissionsGuard = EnsureAllPermissionsExist(distinctPermissionNames, permissions);
        if (!permissionsGuard.IsSuccess)
        {
            return permissionsGuard.ToErrorResult<string>();
        }

        role.UpdateDescription(description);

        var rolePermissions = await _identityDbContext.RolePermissions
            .Where(rolePermission => rolePermission.RoleId == role.Id)
            .ToListAsync(cancellationToken);
        _identityDbContext.RolePermissions.RemoveRange(rolePermissions);

        foreach (var permission in permissions)
        {
            _identityDbContext.RolePermissions.Add(new RolePermission(role.Id, permission.Id)
            {
                Id = _idGenerator.CreateId()
            });
        }

        await _identityDbContext.SaveChangesAsync(cancellationToken);
        return Result<string>.Success(role.Id.ToString());
    }

    private async Task<Result> EnsureCurrentUserCanMutatePlatformAdminRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        if (!SystemRole.IsPlatformAdminRoleName(roleName))
        {
            return Result.Success();
        }

        if (_httpContextAccessor.HttpContext is null)
        {
            return Result.Success();
        }

        if (await CurrentUserIsPlatformAdminAsync(cancellationToken))
        {
            return Result.Success();
        }

        _logger.LogWarning("Blocked PlatformAdmin role mutation by a non-platform administrator.");
        return Result.Forbidden(PlatformAdminRoleMutationForbiddenMessage);
    }

    private async Task<Result> EnsureCurrentUserCanModifyPlatformAdminUserAsync(AppUser user, CancellationToken cancellationToken)
    {
        var targetUserRoles = await _userManager.GetRolesAsync(user);
        if (!targetUserRoles.Any(SystemRole.IsPlatformAdminRoleName))
        {
            return Result.Success();
        }

        if (await CurrentUserIsPlatformAdminAsync(cancellationToken))
        {
            return Result.Success();
        }

        _logger.LogWarning("Blocked PlatformAdmin role mutation: non-platform administrator attempted to replace roles for a PlatformAdmin user.");

        return Result.Forbidden(PlatformAdminUserMutationForbiddenMessage);
    }

    private static Result EnsureExternalUserRolesAreReadOnly(AppUser user)
    {
        return user.IsExternal
            ? Result.Forbidden(ExternalUserRoleMutationForbiddenMessage)
            : Result.Success();
    }

    private async Task<bool> CurrentUserIsPlatformAdminAsync(CancellationToken cancellationToken)
    {
        var isPlatformAdminResult = await _currentUserAuthorizationService.IsPlatformAdminAsync(cancellationToken);
        return isPlatformAdminResult.IsSuccess && isPlatformAdminResult.Value;
    }

    private Task<AppUser?> FindUserByIdAsync(long userId)
    {
        return _userManager.FindByIdAsync(userId.ToString());
    }

    private static Result ValidateRoleMutationInput(long userId, string roleName)
    {
        var userIdGuard = ValidateUserId(userId);
        if (!userIdGuard.IsSuccess)
        {
            return userIdGuard;
        }

        return ValidateRoleName(roleName);
    }

    private static Result ValidateUserId(long userId)
    {
        if (userId > 0)
        {
            return Result.Success();
        }

        return Result.Invalid(new ValidationError
        {
            Identifier = nameof(userId),
            ErrorMessage = "User ID must be greater than zero."
        });
    }

    private static Result ValidateRoleName(string roleName)
    {
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            return Result.Success();
        }

        return Result.Invalid(new ValidationError
        {
            Identifier = nameof(roleName),
            ErrorMessage = "Role name is required."
        });
    }

    private static Result ValidateReplacementRoleNames(IReadOnlyList<string> roleNames)
    {
        if (roleNames is null)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleNames),
                ErrorMessage = "Role names are required."
            });
        }

        if (roleNames.Any(string.IsNullOrWhiteSpace))
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleNames),
                ErrorMessage = "Role names cannot be empty."
            });
        }

        var platformAdminRoles = roleNames
            .Where(SystemRole.IsPlatformAdminRoleName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (platformAdminRoles.Count == 0)
        {
            return Result.Success();
        }

        return Result.Invalid(new ValidationError
        {
            Identifier = nameof(roleNames),
            ErrorMessage = $"The following roles cannot be assigned from tenant user management: {string.Join(", ", platformAdminRoles)}"
        });
    }

    private static Result ValidatePermissionNames(List<string> permissionNames, bool allowEmpty)
    {
        if (permissionNames is null)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(permissionNames),
                ErrorMessage = "Permission names are required."
            });
        }

        if (!allowEmpty && permissionNames.Count == 0)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(permissionNames),
                ErrorMessage = "At least one permission is required."
            });
        }

        if (permissionNames.Any(string.IsNullOrWhiteSpace))
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(permissionNames),
                ErrorMessage = "Permission names cannot be empty."
            });
        }

        return Result.Success();
    }

    private static List<string> GetDistinctPermissionNames(List<string> permissionNames)
    {
        return permissionNames
            .Select(permissionName => permissionName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> GetDistinctRoleNames(IReadOnlyList<string> roleNames)
    {
        return roleNames
            .Select(roleName => roleName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyDictionary<string, int>> LoadExternalRoleUserCountsByNameAsync(
        long tenantId,
        CancellationToken cancellationToken)
    {
        var externalRolesJson = await _identityDbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.TenantId == tenantId &&
                user.AuthProvider != AuthProviders.Endatix &&
                user.ExternalRolesJson != null)
            .Select(user => user.ExternalRolesJson!)
            .ToListAsync(cancellationToken);

        return AggregateExternalRoleUserCounts(externalRolesJson);
    }

    private static Dictionary<string, int> AggregateExternalRoleUserCounts(IEnumerable<string> externalRolesJsonValues)
    {
        Dictionary<string, int> roleCounts = new(StringComparer.OrdinalIgnoreCase);

        foreach (var rolesJson in externalRolesJsonValues)
        {
            foreach (var roleName in DeserializeExternalRoles(rolesJson))
            {
                if (roleCounts.TryGetValue(roleName, out var count))
                {
                    roleCounts[roleName] = count + 1;
                }
                else
                {
                    roleCounts[roleName] = 1;
                }
            }
        }

        return roleCounts;
    }

    private static IReadOnlyList<string> DeserializeExternalRoles(string externalRolesJson)
    {
        try
        {
            var roleNames = JsonSerializer.Deserialize<string[]>(externalRolesJson, _externalRolesJsonSerializerOptions);
            if (roleNames is null || roleNames.Length == 0)
            {
                return [];
            }

            HashSet<string> distinctRoleNames = new(StringComparer.OrdinalIgnoreCase);
            foreach (var roleName in roleNames)
            {
                if (!string.IsNullOrWhiteSpace(roleName))
                {
                    distinctRoleNames.Add(roleName.Trim());
                }
            }

            return distinctRoleNames.Count == 0 ? [] : [.. distinctRoleNames];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record RoleHeaderRow(
        long Id,
        string Name,
        string? Description,
        bool IsSystemDefined,
        bool IsActive);

    private sealed record RolePermissionNameRow(long RoleId, string Name);

    private sealed record PersistedRoleRow(
        long Id,
        string Name,
        string? Description,
        bool IsSystemDefined,
        bool IsActive,
        List<string> PermissionNames);

    private static Result EnsureAllPermissionsExist(IReadOnlyCollection<string> requestedPermissionNames, IReadOnlyCollection<Permission> permissions)
    {
        var foundPermissionNames = permissions
            .Select(permission => permission.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingPermissionNames = requestedPermissionNames
            .Where(permissionName => !foundPermissionNames.Contains(permissionName))
            .ToList();

        if (missingPermissionNames.Count == 0)
        {
            return Result.Success();
        }

        return Result.Invalid(new ValidationError
        {
            Identifier = "permissionNames",
            ErrorMessage = $"The following permissions do not exist: {string.Join(", ", missingPermissionNames)}"
        });
    }

    private static Result EnsureAllRolesExist(IReadOnlyCollection<string> requestedRoleNames, IReadOnlyCollection<AppRole> roles)
    {
        var foundRoleNames = roles
            .Select(role => role.Name)
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
            .Select(roleName => roleName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingRoleNames = requestedRoleNames
            .Where(roleName => !foundRoleNames.Contains(roleName))
            .ToList();

        if (missingRoleNames.Count == 0)
        {
            return Result.Success();
        }

        return Result.Invalid(new ValidationError
        {
            Identifier = "roleNames",
            ErrorMessage = $"The following roles do not exist: {string.Join(", ", missingRoleNames)}"
        });
    }

    private static string NormalizeRoleName(string roleName)
    {
        return roleName.ToUpperInvariant();
    }

    private static Result UserNotFound(long userId)
    {
        return Result.NotFound($"User with ID {userId} not found.");
    }
}

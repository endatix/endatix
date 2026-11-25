using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.ListRoles;
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
public sealed partial class RoleManagementService : IRoleManagementService, ISystemRoleAssignment
{
    private const string PLATFORM_ADMIN_ROLE_MUTATION_FORBIDDEN_MESSAGE = "Only platform administrators can assign or remove the PlatformAdmin role.";
    private const string PLATFORM_ADMIN_USER_MUTATION_FORBIDDEN_MESSAGE = "Only platform administrators can modify users with the PlatformAdmin role.";
    private const string EXTERNAL_USER_ROLE_MUTATION_FORBIDDEN_MESSAGE = "External users receive roles from their identity provider and cannot be edited locally.";

    private static readonly IReadOnlyDictionary<string, SystemRole> _persistedSystemRolesByName =
        SystemRole.AllSystemRoles.ToDictionary(role => role.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions _externalRolesJsonSerializerOptions = new();

    private readonly UserManager<AppUser> _userManager;
    private readonly AppIdentityDbContext _identityDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRolesRepository _rolesRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserAuthorizationService _currentUserAuthorizationService;
    private readonly IAuthorizationCache _authorizationCache;
    private readonly ILogger<RoleManagementService> _logger;
    private readonly RoleGrantCeiling _ceiling;

    [SuppressMessage("csharpsquid", "S107:Methods should not have too many parameters", Justification = "Public DI constructor consumed by endatix-saas; grouping the collaborators into holders would break it and adds types with no behaviour.")]
    public RoleManagementService(
        UserManager<AppUser> userManager,
        AppIdentityDbContext identityDbContext,
        ITenantContext tenantContext,
        IRolesRepository rolesRepository,
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserAuthorizationService currentUserAuthorizationService,
        IAuthorizationCache authorizationCache,
        ILogger<RoleManagementService> logger)
    {
        _userManager = userManager;
        _identityDbContext = identityDbContext;
        _tenantContext = tenantContext;
        _rolesRepository = rolesRepository;
        _httpContextAccessor = httpContextAccessor;
        _currentUserAuthorizationService = currentUserAuthorizationService;
        _authorizationCache = authorizationCache;
        _logger = logger;
        _ceiling = new RoleGrantCeiling(identityDbContext, httpContextAccessor, currentUserAuthorizationService, logger);
    }

    /// <inheritdoc/>
    public async Task<Result> AssignRoleToUserAsync(long userId, string roleName, CancellationToken cancellationToken = default)
    {
        var (user, guard) = await ResolveUserRoleChangeAsync(userId, roleName, cancellationToken);
        if (user is null)
        {
            return guard;
        }

        return await AddRoleInScopeAsync(user.Id, roleName.Trim(), _tenantContext.TenantId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result> AssignRoleAsSystemAsync(long userId, string roleName, CancellationToken cancellationToken = default)
    {
        var inputGuard = ValidateRoleMutationInput(userId, roleName);
        if (!inputGuard.IsSuccess)
        {
            return inputGuard;
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

        return await AddRoleInScopeAsync(user.Id, roleName.Trim(), user.TenantId, cancellationToken);
    }

    /// <summary>
    /// The checks a user-initiated assign or remove passes before it runs: a signed-in caller who is not the target,
    /// a role within the caller's own access, a target in the current tenant, and the PlatformAdmin and
    /// external-user guards.
    /// </summary>
    private async Task<(AppUser? User, Result Guard)> ResolveUserRoleChangeAsync(
        long userId,
        string roleName,
        CancellationToken cancellationToken)
    {
        var inputGuard = ValidateRoleMutationInput(userId, roleName);
        if (!inputGuard.IsSuccess)
        {
            return (null, inputGuard);
        }

        var callerGuard = await EnsureCallerMayChangeRolesAsync(userId, [roleName.Trim()], cancellationToken);
        if (!callerGuard.IsSuccess)
        {
            return (null, callerGuard);
        }

        var user = await FindUserByIdAsync(userId);
        if (user is null || user.TenantId != _tenantContext.TenantId)
        {
            return (null, UserNotFound(userId));
        }

        var externalUserGuard = EnsureExternalUserRolesAreReadOnly(user);
        return externalUserGuard.IsSuccess ? (user, externalUserGuard) : (null, externalUserGuard);
    }

    private async Task<Result> EnsureCallerMayChangeRolesAsync(
        long userId,
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken)
    {
        var ownRolesGuard = await _ceiling.EnsureNotOwnRolesAsync(userId, cancellationToken);
        if (!ownRolesGuard.IsSuccess)
        {
            return ownRolesGuard;
        }

        foreach (var roleName in roleNames)
        {
            var platformAdminRoleGuard = await EnsureCurrentUserCanMutatePlatformAdminRoleAsync(roleName, cancellationToken);
            if (!platformAdminRoleGuard.IsSuccess)
            {
                return platformAdminRoleGuard;
            }
        }

        return await EnsureCurrentUserCanGrantRolesAsync(roleNames, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Result> EnsureCurrentUserCanGrantRolesAsync(IReadOnlyList<string> roleNames, CancellationToken cancellationToken = default)
    {
        var normalizedRoleNames = GetDistinctRoleNames(roleNames).Select(NormalizeRoleName).ToList();
        return _ceiling.EnsureCanGrantRoleNamesAsync(normalizedRoleNames, _tenantContext.TenantId, cancellationToken);
    }

    /// <summary>
    /// The role a name means in a tenant: the tenant's own role, else the built-in system row. Role changes resolve
    /// the name to that role's id and act on the id.
    /// </summary>
    private async Task<long?> FindRoleIdInScopeAsync(string roleName, long tenantId, CancellationToken cancellationToken)
    {
        var normalizedRoleName = NormalizeRoleName(roleName);
        var candidates = await _identityDbContext.Roles
            .AsNoTracking()
            .Where(role => role.NormalizedName == normalizedRoleName &&
                (role.TenantId == tenantId || (role.IsSystemDefined && role.TenantId <= 0)))
            .Select(role => new { role.Id, role.TenantId })
            .ToListAsync(cancellationToken);
        return candidates
            .OrderByDescending(role => role.TenantId == tenantId)
            .Select(role => (long?)role.Id)
            .FirstOrDefault();
    }

    private async Task<Result> AddRoleInScopeAsync(long userId, string roleName, long tenantId, CancellationToken cancellationToken)
    {
        var roleId = await FindRoleIdInScopeAsync(roleName, tenantId, cancellationToken);
        if (roleId is null)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"Role '{roleName}' does not exist."
            });
        }

        var isInRole = await _identityDbContext.UserRoles
            .AnyAsync(userRole => userRole.UserId == userId && userRole.RoleId == roleId, cancellationToken);
        if (isInRole)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"User already has role '{roleName}'."
            });
        }

        _identityDbContext.UserRoles.Add(new IdentityUserRole<long> { UserId = userId, RoleId = roleId.Value });
        await _identityDbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> RemoveRoleFromUserAsync(long userId, string roleName, CancellationToken cancellationToken = default)
    {
        var (user, guard) = await ResolveUserRoleChangeAsync(userId, roleName, cancellationToken);
        if (user is null)
        {
            return guard;
        }

        var trimmedRoleName = roleName.Trim();
        var roleId = await FindRoleIdInScopeAsync(trimmedRoleName, _tenantContext.TenantId, cancellationToken);
        var userRole = roleId is null
            ? null
            : await _identityDbContext.UserRoles
                .FirstOrDefaultAsync(item => item.UserId == user.Id && item.RoleId == roleId, cancellationToken);
        if (userRole is null)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(roleName),
                ErrorMessage = $"User does not have role '{trimmedRoleName}'."
            });
        }

        _identityDbContext.UserRoles.Remove(userRole);
        await _identityDbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> GrantPlatformAdminAsync(long userId, CancellationToken cancellationToken = default)
    {
        var userIdGuard = ValidateUserId(userId);
        if (!userIdGuard.IsSuccess)
        {
            return userIdGuard;
        }

        var mutationGuard = await EnsureCurrentUserCanMutatePlatformAdminRoleAsync(SystemRole.PlatformAdmin.Name, cancellationToken);
        if (!mutationGuard.IsSuccess)
        {
            return mutationGuard;
        }

        var user = await FindUserByIdAsync(userId);
        if (user is null)
        {
            return UserNotFound(userId);
        }

        var isInRole = await _userManager.IsInRoleAsync(user, SystemRole.PlatformAdmin.Name);
        if (isInRole)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(userId),
                ErrorMessage = "User is already a platform administrator."
            });
        }

        return await ChangePlatformRoleAsync(user, _userManager.AddToRoleAsync, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result> RevokePlatformAdminAsync(long userId, CancellationToken cancellationToken = default)
    {
        var userIdGuard = ValidateUserId(userId);
        if (!userIdGuard.IsSuccess)
        {
            return userIdGuard;
        }

        var mutationGuard = await EnsureCurrentUserCanMutatePlatformAdminRoleAsync(SystemRole.PlatformAdmin.Name, cancellationToken);
        if (!mutationGuard.IsSuccess)
        {
            return mutationGuard;
        }

        if (IsCurrentUser(userId))
        {
            return Result.Forbidden("You cannot revoke your own PlatformAdmin role.");
        }

        var user = await FindUserByIdAsync(userId);
        if (user is null)
        {
            return UserNotFound(userId);
        }

        var isInRole = await _userManager.IsInRoleAsync(user, SystemRole.PlatformAdmin.Name);
        if (!isInRole)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(userId),
                ErrorMessage = "User is not a platform administrator."
            });
        }

        if (!await HasAnotherActivePlatformAdminAsync(userId, cancellationToken))
        {
            return Result.Conflict("Cannot revoke the last active platform administrator.");
        }

        return await ChangePlatformRoleAsync(user, _userManager.RemoveFromRoleAsync, cancellationToken);
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

        var changedRoles = currentEditableRoleAssignments
            .Where(item => !requestedEditableRoleIds.Contains(item.UserRole.RoleId))
            .Select(item => item.Role)
            .Concat(requestedEditableRoles.Where(role => missingEditableRoleIds.Contains(role.Id)))
            .ToList();
        var accessGuard = await EnsureCanReplaceRolesAsync(userId, changedRoles, cancellationToken);
        if (!accessGuard.IsSuccess)
        {
            return accessGuard;
        }

        var newAssignments = missingEditableRoleIds.Select(roleId => new IdentityUserRole<long> { UserId = userId, RoleId = roleId });
        return await _standing.RefusalAsync(cancellationToken)
            ?? await SaveAssignmentsAsync(staleEditableRoleAssignments, newAssignments, cancellationToken);
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

        var distinctPermissionNames = GetDistinctPermissionNames(permissionNames);
        var accessGuard = await EnsureCanSetRolePermissionsAsync(distinctPermissionNames, [], cancellationToken);
        if (!accessGuard.IsSuccess)
        {
            return accessGuard.ToErrorResult<string>();
        }

        if (await _standing.RefusalAsync<string>(cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var tenantId = _tenantContext.TenantId;
        var roleName = name.Trim();
        var normalizedRoleName = NormalizeRoleName(roleName);

        // Compared on the normalized key role lookups use, so a custom role never shares a system role's name.
        if (SystemRole.AllSystemRoleNames.Any(systemRoleName => NormalizeRoleName(systemRoleName) == normalizedRoleName))
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

        var permissions = await ReadRolePermissionsAsync(distinctPermissionNames, cancellationToken);
        if (!permissions.IsSuccess)
        {
            return permissions.ToErrorResult<string>();
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

        var permissionIds = permissions.Value.Select(p => p.Id).ToList();
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

        return await _standing.RefusalAsync(cancellationToken) ?? await DeleteCustomRoleAsync(role, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<Paged<RoleListItem>>> ListRolesAsync(
        SearchablePageRequest paging,
        RoleListCriteria criteria,
        CancellationToken cancellationToken = default)
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
        allItems = ApplyRoleListFilters(allItems, criteria.RoleType, paging.Search);
        allItems = ApplyRoleListOrdering(allItems, criteria.Sort);

        var resolved = paging.Paging.ForTotal(allItems.Count);
        var pagedItems = allItems.Skip(resolved.Skip).Take(resolved.PageSize).ToList();

        return Result<Paged<RoleListItem>>.Success(resolved.ToPaged(pagedItems));
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<string>>> GetMissingAssignableRoleNamesAsync(
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken = default) =>
        GetMissingAssignableRoleNamesAsync(roleNames, _tenantContext.TenantId, cancellationToken);

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<string>>> GetMissingAssignableRoleNamesAsync(
        IReadOnlyList<string> roleNames,
        long tenantId,
        CancellationToken cancellationToken = default)
    {
        if (roleNames.Count == 0)
        {
            return Result<IReadOnlyList<string>>.Success([]);
        }

        var requestedRoleNames = GetDistinctRoleNames(roleNames);
        var requestedNormalizedRoleNames = requestedRoleNames
            .Select(NormalizeRoleName)
            .ToList();

        var persistedNames = await _identityDbContext.Roles
            .AsNoTracking()
            .Where(role =>
                role.IsActive &&
                role.NormalizedName != null &&
                requestedNormalizedRoleNames.Contains(role.NormalizedName) &&
                (role.TenantId == tenantId ||
                 (role.IsSystemDefined && role.TenantId <= 0 && role.Name != SystemRole.PlatformAdmin.Name)))
            .Select(role => role.Name)
            .ToListAsync(cancellationToken);

        var knownNames = persistedNames
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var systemRole in SystemRole.AllSystemRoles)
        {
            if (systemRole.IsPersisted && !SystemRole.IsPlatformAdminRoleName(systemRole.Name))
            {
                knownNames.Add(systemRole.Name);
            }
        }

        IReadOnlyList<string> missingRoleNames = requestedRoleNames
            .Where(roleName => !knownNames.Contains(roleName))
            .ToList();

        return Result<IReadOnlyList<string>>.Success(missingRoleNames);
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

    private static List<RoleListItem> ApplyRoleListOrdering(
        List<RoleListItem> roles,
        SortRequest<RoleListSortBy>? sort)
    {
        if (sort is null)
        {
            return [.. roles.OrderBy(role => role.Name, StringComparer.OrdinalIgnoreCase)];
        }

        var sortDescending = sort.IsDescending;
        var ordered = sort.Field switch
        {
            RoleListSortBy.IsActive when sortDescending =>
                roles.OrderByDescending(role => role.IsActive).ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase),
            RoleListSortBy.IsActive =>
                roles.OrderBy(role => role.IsActive).ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase),
            RoleListSortBy.UsersCount when sortDescending =>
                roles.OrderByDescending(role => role.UsersCount).ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase),
            RoleListSortBy.UsersCount =>
                roles.OrderBy(role => role.UsersCount).ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase),
            RoleListSortBy.Name when sortDescending =>
                roles.OrderByDescending(role => role.Name, StringComparer.OrdinalIgnoreCase),
            _ =>
                roles.OrderBy(role => role.Name, StringComparer.OrdinalIgnoreCase),
        };

        return ordered.ToList();
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<PermissionListItem>>> ListPermissionsAsync(
        CancellationToken cancellationToken = default)
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

        var tenantRole = _identityDbContext.Roles.Where(role => role.NormalizedName == normalizedRoleName && role.TenantId == tenantId);
        var target = await AuthorizeRoleRewriteAsync(tenantRole, distinctPermissionNames, trimmedRoleName, cancellationToken);
        if (!target.IsSuccess)
        {
            return target.ToErrorResult<string>();
        }

        if (await _standing.RefusalAsync<string>(cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var role = await tenantRole.FirstOrDefaultAsync(role => role.Id == target.Value && !role.IsSystemDefined, cancellationToken);
        if (role is null)
        {
            return Result.NotFound($"Role '{trimmedRoleName}' not found for this tenant.");
        }

        var permissions = await ReadRolePermissionsAsync(distinctPermissionNames, cancellationToken);
        if (!permissions.IsSuccess)
        {
            return permissions.ToErrorResult<string>();
        }

        role.UpdateDescription(description);

        var rolePermissions = await _identityDbContext.RolePermissions
            .Where(rolePermission => rolePermission.RoleId == role.Id)
            .ToListAsync(cancellationToken);
        _identityDbContext.RolePermissions.RemoveRange(rolePermissions);

        foreach (var permission in permissions.Value)
        {
            _identityDbContext.RolePermissions.Add(new RolePermission(role.Id, permission.Id));
        }

        await _identityDbContext.SaveChangesAsync(cancellationToken);
        return Result<string>.Success(role.Id.ToString());
    }

    /// <summary>
    /// Replacing a user's roles: the target is not the caller, and the caller holds every role added or removed.
    /// </summary>
    private async Task<Result> EnsureCanReplaceRolesAsync(
        long userId,
        IReadOnlyCollection<AppRole> changedRoles,
        CancellationToken cancellationToken)
    {
        var ownRolesGuard = await _ceiling.EnsureNotOwnRolesAsync(userId, cancellationToken);
        return ownRolesGuard.IsSuccess
            ? await _ceiling.EnsureCanGrantRolesAsync(changedRoles, cancellationToken)
            : ownRolesGuard;
    }

    /// <summary>
    /// Changing a role's permissions: the caller does not hold the role, and holds both its new and its current
    /// permissions.
    /// </summary>
    private async Task<Result> EnsureCanRewriteRoleAsync(
        long roleId,
        IReadOnlyList<string> requestedPermissionNames,
        CancellationToken cancellationToken)
    {
        var ownRoleGuard = await _ceiling.EnsureNotOwnRoleAsync(roleId, cancellationToken);
        if (!ownRoleGuard.IsSuccess)
        {
            return ownRoleGuard;
        }

        var currentPermissionNames = await _identityDbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => rolePermission.RoleId == roleId && rolePermission.IsActive)
            .Select(rolePermission => rolePermission.Permission.Name)
            .ToListAsync(cancellationToken);
        return await EnsureCanSetRolePermissionsAsync(requestedPermissionNames, currentPermissionNames, cancellationToken);
    }

    /// <summary>
    /// A tenant role carries no platform permission, and the caller holds both what the role gets and what it had,
    /// so they can neither widen a role nor narrow one wider than themselves.
    /// </summary>
    private async Task<Result> EnsureCanSetRolePermissionsAsync(
        IReadOnlyList<string> requestedPermissionNames,
        IReadOnlyList<string> currentPermissionNames,
        CancellationToken cancellationToken)
    {
        var platformGuard = RoleGrantCeiling.EnsureNoPlatformPermissions(requestedPermissionNames, "permissionNames");
        if (!platformGuard.IsSuccess)
        {
            return platformGuard;
        }

        return await _ceiling.EnsureCanGrantPermissionsAsync(
            requestedPermissionNames.Concat(currentPermissionNames),
            cancellationToken);
    }

    private async Task<Result> EnsureCurrentUserCanMutatePlatformAdminRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        if (!SystemRole.IsPlatformAdminRoleName(roleName))
        {
            return Result.Success();
        }

        if (await CurrentUserIsPlatformAdminAsync(cancellationToken))
        {
            return Result.Success();
        }

        _logger.LogWarning("Blocked PlatformAdmin role mutation by a non-platform administrator.");
        return Result.Forbidden(PLATFORM_ADMIN_ROLE_MUTATION_FORBIDDEN_MESSAGE);
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

        return Result.Forbidden(PLATFORM_ADMIN_USER_MUTATION_FORBIDDEN_MESSAGE);
    }

    private static Result EnsureExternalUserRolesAreReadOnly(AppUser user)
    {
        return user.IsExternal
            ? Result.Forbidden(EXTERNAL_USER_ROLE_MUTATION_FORBIDDEN_MESSAGE)
            : Result.Success();
    }

    private async Task<bool> CurrentUserIsPlatformAdminAsync(CancellationToken cancellationToken)
    {
        var isPlatformAdminResult = await _currentUserAuthorizationService.IsPlatformAdminAsync(cancellationToken);
        return isPlatformAdminResult.IsSuccess && isPlatformAdminResult.Value;
    }

    private bool IsCurrentUser(long userId)
    {
        var currentUserId = _httpContextAccessor.HttpContext?.User.GetUserId();
        return long.TryParse(currentUserId, out var currentUserIdValue) && currentUserIdValue == userId;
    }

    private async Task<bool> HasAnotherActivePlatformAdminAsync(long removedUserId, CancellationToken cancellationToken)
    {
        var normalizedPlatformAdminRoleName = NormalizeRoleName(SystemRole.PlatformAdmin.Name);

        return await _identityDbContext.UserRoles
            .Where(userRole => userRole.UserId != removedUserId)
            .Join(
                _identityDbContext.Roles.Where(role =>
                    role.IsActive &&
                    role.IsSystemDefined &&
                    role.TenantId <= 0 &&
                    role.NormalizedName == normalizedPlatformAdminRoleName),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, _) => userRole.UserId)
            .Join(
                _identityDbContext.Users.Where(user =>
                    user.TenantId > 0 &&
                    (user.EmailConfirmed || user.AuthProvider != AuthProviders.Endatix) &&
                    (user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow)),
                userId => userId,
                user => user.Id,
                (_, _) => true)
            .AnyAsync(cancellationToken);
    }

    private async Task InvalidateUserAuthorizationAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (user.TenantId <= 0)
        {
            return;
        }

        await _authorizationCache.InvalidateAsync(user.Id.ToString(), user.TenantId, cancellationToken);
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
            foreach (var roleName in roleNames.Where(roleName => !string.IsNullOrWhiteSpace(roleName)))
            {
                distinctRoleNames.Add(roleName.Trim());
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

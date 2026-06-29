using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Endatix.Infrastructure.Identity.Authorization.IExternalAuthorizationMapper;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Default implementation of IExternalAuthorizationMapper.
/// </summary>
/// <param name="roleManager">The role manager to query roles.</param>
/// <param name="keyNormalizer">The key normalizer for normalizing role names.</param>
internal sealed class DefaultAuthorizationMapper(
        RoleManager<AppRole> roleManager,
        ILookupNormalizer keyNormalizer
) : IExternalAuthorizationMapper
{

    /// <inheritdoc />
    public async Task<MappingResult> MapToAppRolesAsync(string[] externalRoles, Dictionary<string, string> roleMappings, CancellationToken cancellationToken)
    {
        var normalizedMappingsResult = NormalizeRoleMappings(roleMappings);
        if (!normalizedMappingsResult.IsSuccess)
        {
            return normalizedMappingsResult;
        }

        var matchingRoles = GetMatchingRoles(externalRoles, normalizedMappingsResult.RoleMappings);
        if (matchingRoles is not { Length: > 0 })
        {
            return MappingResult.Empty();
        }

        var normalizedRoleNames = matchingRoles
            .Select(role => keyNormalizer.NormalizeName(role))
            .Distinct()
            .ToArray();

        try
        {
            var mappedRolesWithPermissions = await roleManager.Roles
                            .Where(x => x.IsActive && normalizedRoleNames.Contains(x.NormalizedName))
                            .Include(x => x.RolePermissions)
                                .ThenInclude(x => x.Permission)
                            .AsSplitQuery()
                            .AsNoTracking()
                            .ToListAsync(cancellationToken);

            var roleNames = mappedRolesWithPermissions
                .Where(role => role.Name is not null)
                .Select(role => role.Name!)
                .Distinct()
                .ToArray();

            var permissions = mappedRolesWithPermissions
                .SelectMany(x => x.RolePermissions)
                .Where(x => x.IsActive && x.Permission is not null && !string.IsNullOrEmpty(x.Permission.Name))
                .Select(x => x.Permission.Name)
                .Distinct()
                .ToArray();

            return MappingResult.Success(roleNames, permissions);
        }
        catch (Exception ex)
        {
            return MappingResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Gets the matching roles based on the external roles and role mappings.
    /// </summary>
    /// <param name="externalRoles">The external roles to get the matching roles from.</param>
    /// <param name="roleMappings">The role mappings to use.</param>
    /// <returns>The matching roles.</returns>
    private string[] GetMatchingRoles(string[] externalRoles, Dictionary<string, string> roleMappings)
    {
        if (externalRoles is not { Length: > 0 })
        {
            return [];
        }

        if (roleMappings is not { Count: > 0 })
        {
            return [];
        }

        HashSet<string> matchingRoles = new(StringComparer.OrdinalIgnoreCase);
        foreach (var externalRole in externalRoles)
        {
            if (roleMappings.TryGetValue(externalRole, out var matchingRole) && !string.IsNullOrEmpty(matchingRole))
            {
                matchingRoles.Add(matchingRole);
            }
        }

        return matchingRoles.ToArray();
    }

    private static NormalizedRoleMappingsResult NormalizeRoleMappings(Dictionary<string, string> roleMappings)
    {
        if (roleMappings is not { Count: > 0 })
        {
            return NormalizedRoleMappingsResult.Success([]);
        }

        var ambiguousKeys = roleMappings.Keys
            .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(", ", group.OrderBy(key => key, StringComparer.Ordinal)))
            .ToList();

        if (ambiguousKeys.Count > 0)
        {
            return NormalizedRoleMappingsResult.Failure(
                $"External role mappings contain ambiguous case-insensitive keys: {string.Join("; ", ambiguousKeys)}.");
        }

        return NormalizedRoleMappingsResult.Success(new Dictionary<string, string>(
            roleMappings,
            StringComparer.OrdinalIgnoreCase));
    }

    private sealed record NormalizedRoleMappingsResult
    {
        private NormalizedRoleMappingsResult(
            bool isSuccess,
            Dictionary<string, string> roleMappings,
            string? errorMessage = null)
        {
            IsSuccess = isSuccess;
            RoleMappings = roleMappings;
            ErrorMessage = errorMessage;
        }

        public bool IsSuccess { get; }
        public Dictionary<string, string> RoleMappings { get; }
        public string? ErrorMessage { get; }

        public static NormalizedRoleMappingsResult Success(Dictionary<string, string> roleMappings) =>
            new(true, roleMappings);

        public static NormalizedRoleMappingsResult Failure(string errorMessage) =>
            new(false, [], errorMessage);

        public static implicit operator MappingResult(NormalizedRoleMappingsResult result) =>
            MappingResult.Failure(result.ErrorMessage ?? "Invalid external role mappings.");
    }
}

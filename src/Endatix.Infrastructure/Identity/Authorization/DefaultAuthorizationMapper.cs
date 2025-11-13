using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Endatix.Infrastructure.Identity.Authorization.IExternalAuthorizationMapper;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Default implementation of IExternalAuthorizationMapper.
/// </summary>
/// <param name="dbContext">The identity database context to query roles.</param>
/// <param name="keyNormalizer">The key normalizer for normalizing role names.</param>
public class DefaultAuthorizationMapper(
        AppIdentityDbContext dbContext,
        ILookupNormalizer keyNormalizer
) : IExternalAuthorizationMapper
{

    /// <inheritdoc />
    public async Task<MappingResult> MapToAppRolesAsync(string[] externalRoles, Dictionary<string, string> roleMappings, CancellationToken cancellationToken)
    {

        if (externalRoles is not { Length: > 0 })
        {
            return MappingResult.Empty();
        }

        if (roleMappings is not { Count: > 0 })
        {
            return MappingResult.Empty();
        }

        try
        {
            var matchingRoles = externalRoles
                .Where(er => roleMappings.TryGetValue(er, out var matchingRole) && !string.IsNullOrEmpty(matchingRole))
                .Select(roleName => keyNormalizer.NormalizeName(roleName))
                .Distinct()
                .ToArray();

            var mappedRolesWithPermissions = await dbContext.Roles
                            .Where(x => x.IsActive && matchingRoles.Contains(x.NormalizedName))
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
}

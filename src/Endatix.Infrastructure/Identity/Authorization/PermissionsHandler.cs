using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Authorization handler that checks admin, direct and ownership permissions, and grants/denies access
/// Uses URL parsing to extract entity type and ID from request path for checking ownership
/// </summary>
public class PermissionsHandler : IAuthorizationHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _dbContext;
    private readonly IUserContext _userContext;

    private record EntityMetadata(Type EntityType, PermissionCategory PermissionCategory);

    private readonly Dictionary<string, EntityMetadata> _urlSegmentToEntityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["forms"] = new(typeof(Form), PermissionCategory.Forms),
        ["submissions"] = new(typeof(Submission), PermissionCategory.Submissions),
        ["form-templates"] = new(typeof(FormTemplate), PermissionCategory.Templates),
        ["themes"] = new(typeof(Theme), PermissionCategory.Themes),
        ["questions"] = new(typeof(CustomQuestion), PermissionCategory.Questions)
    };

    private static readonly MemoryCacheEntryOptions OwnershipCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        SlidingExpiration = TimeSpan.FromMinutes(2)
    };

    public PermissionsHandler(
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache cache,
        AppDbContext dbContext,
        IUserContext userContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _dbContext = dbContext;
        _userContext = userContext;
    }

    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.HasSucceeded)
        {
            return;
        }

        var isAdmin = CheckIsAdmin(context);
        if (isAdmin)
        {
            SucceedAuthorization(context);
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        var endpointPermissions = GetEndpointPermissions(httpContext);
        if (!endpointPermissions.Any())
        {
            return;
        }

        var userPermissions = context.User.FindAll(ClaimNames.Permission).Select(c => c.Value);

        var hasDirectPermission = CheckDirectPermissions(endpointPermissions, userPermissions);
        if (hasDirectPermission)
        {
            SucceedAuthorization(context);
            return;
        }

        var hasOwnershipPermission = await CheckOwnershipPermissions(httpContext, endpointPermissions, userPermissions);
        if (hasOwnershipPermission)
        {
            SucceedAuthorization(context);
            return;
        }

        context.Fail();
    }

    private bool CheckIsAdmin(AuthorizationHandlerContext context)
    {
        var isAdminClaimValue = context.User.FindFirst(ClaimNames.IsAdmin)?.Value;
        var isAdmin = !string.IsNullOrEmpty(isAdminClaimValue) && isAdminClaimValue.Equals("true", StringComparison.OrdinalIgnoreCase);
        return isAdmin;
    }

    private void SucceedAuthorization(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.Requirements)
        {
            context.Succeed(requirement);
        }
    }

    private IEnumerable<string> GetEndpointPermissions(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint == null)
        {
            return [];
        }

        var endpointDefinition = endpoint.Metadata.OfType<FastEndpoints.EndpointDefinition>().FirstOrDefault();
        if (endpointDefinition?.AllowedPermissions != null)
        {
            return endpointDefinition.AllowedPermissions;
        }

        return [];
    }

    private bool CheckDirectPermissions(IEnumerable<string> endpointPermissions, IEnumerable<string> userPermissions)
    {
        var directPermissions = endpointPermissions.Where(p => !p.EndsWith(".owned", StringComparison.OrdinalIgnoreCase));
        if (directPermissions.Any(dp => userPermissions.Contains(dp)))
        {
            return true;
        }

        return false;
    }

    private async Task<bool> CheckOwnershipPermissions(HttpContext httpContext, IEnumerable<string> endpointPermissions, IEnumerable<string> userPermissions)
    {
        var (entityMetadata, entityId) = ParseEntityFromUrl(httpContext.Request.Path);
        if (entityMetadata == null || entityId == null)
        {
            return false; // No entity in the URL so skip ownership check
        }

        var ownershipPermissions = endpointPermissions.Where(p =>
            p.StartsWith(entityMetadata.PermissionCategory.Code, StringComparison.OrdinalIgnoreCase) &&
            p.EndsWith(".owned", StringComparison.OrdinalIgnoreCase));

        if (!ownershipPermissions.Any(op => userPermissions.Contains(op)))
        {
            return false;
        }

        var userId = _userContext.GetCurrentUserId();
        if (userId == null)
        {
            return false; // No current user so skip ownership check
        }

        var isOwner = await UserOwnsEntityCached(userId, entityMetadata.EntityType, entityId);
        return isOwner;
    }

    /// <summary>
    /// URL parsing to extract entity type and ID
    /// Algorithm: Use the last numeric segment as ID and the segment before it as type
    /// </summary>
    private (EntityMetadata? entityMetadata, string? entityId) ParseEntityFromUrl(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = segments.Length - 1; i > 0; i--)
        {
            if (long.TryParse(segments[i], out _))
            {
                var entityUrlSegment = segments[i - 1];
                if (_urlSegmentToEntityMap.TryGetValue(entityUrlSegment, out var metadata))
                {
                    return (metadata, segments[i]);
                }
            }
        }

        return (null, null);
    }

    private async Task<bool> UserOwnsEntityCached(string userId, Type entityType, string entityId)
    {
        var cacheKey = $"ownership_{userId}_{entityType.Name}_{entityId}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetOptions(OwnershipCacheOptions);
            return await UserOwnsEntity(userId, entityType, entityId);
        });
    }

    private async Task<bool> UserOwnsEntity(string userId, Type entityType, string entityId)
    {
        if (!typeof(IOwnedEntity).IsAssignableFrom(entityType))
        {
            return false;
        }

        if (!long.TryParse(entityId, out var parsedEntityId))
        {
            return false;
        }

        var entity = await _dbContext.FindAsync(entityType, parsedEntityId);
        if (entity == null)
        {
            return false;
        }

        if (entity is IOwnedEntity ownedEntity)
        {
            return ownedEntity.IsOwnedBy(userId);
        }

        return false;
    }
}

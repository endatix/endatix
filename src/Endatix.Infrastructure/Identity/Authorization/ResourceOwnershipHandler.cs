using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Authorization handler that grants access to all requirements for users that own entity when permissions ending with ".owned" are present
/// Uses URL parsing to extract entity type and ID from request path
/// </summary>
public class EntityOwnershipHandler : IAuthorizationHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _dbContext;
    private readonly IUserContext _userContext;

    private readonly Dictionary<string, Type> _urlSegmentToTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["forms"] = typeof(Form),
        ["submissions"] = typeof(Submission),
        ["form-templates"] = typeof(FormTemplate),
        ["themes"] = typeof(Theme),
        ["questions"] = typeof(CustomQuestion)
    };

    private static readonly MemoryCacheEntryOptions OwnershipCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        SlidingExpiration = TimeSpan.FromMinutes(2)
    };

    public EntityOwnershipHandler(
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

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        var ownedPermissions = GetEndpointOwnedPermissions(httpContext);
        if (!ownedPermissions.Any())
        {
            return; // No owned permissions so skip ownership check
        }

        var (entityType, entityId) = ParseEntityFromUrl(httpContext.Request.Path);
        if (entityType == null || entityId == null)
        {
            return; // No entity in the URL so skip ownership check
        }

        var userId = _userContext.GetCurrentUserId();
        if (userId == null)
        {
            return; // No current user so skip ownership check
        }

        var isOwner = await UserOwnsEntityCached(userId, entityType, entityId);
        if (isOwner)
        {
            foreach (var requirement in context.Requirements)
            {
                context.Succeed(requirement);
            }
        }
    }

    private List<string> GetEndpointOwnedPermissions(HttpContext httpContext)
    {
        var ownedPermissions = new List<string>();

        var endpoint = httpContext.GetEndpoint();
        if (endpoint == null)
        {
            return ownedPermissions;
        }

        var endpointDefinition = endpoint.Metadata.OfType<FastEndpoints.EndpointDefinition>().FirstOrDefault();
        if (endpointDefinition?.AllowedPermissions != null)
        {
            ownedPermissions = endpointDefinition.AllowedPermissions
                .Where(p => p.EndsWith(".owned", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return ownedPermissions;
    }

    /// <summary>
    /// URL parsing to extract entity type and ID
    /// Algorithm: Use the last numeric segment as ID and the segment before it as type
    /// </summary>
    private (Type? entityType, string? entityId) ParseEntityFromUrl(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            if (long.TryParse(segments[i], out _) && i > 0)
            {
                var entityUrlSegment = segments[i - 1];
                if (_urlSegmentToTypeMap.TryGetValue(entityUrlSegment, out var entityType))
                {
                    return (entityType, segments[i]);
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

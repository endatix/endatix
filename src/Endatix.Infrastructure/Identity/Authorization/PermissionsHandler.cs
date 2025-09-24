using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Attributes;
using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Authorization handler that checks admin, direct and ownership permissions, and grants/denies access
/// Uses URL parsing to extract entity type and ID from request path for checking ownership
/// </summary>
public class PermissionsHandler : IAuthorizationHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HybridCache _cache;
    private readonly AppDbContext _dbContext;
    private readonly IUserContext _userContext;
    private readonly IPermissionService _permissionService;


    private static readonly TimeSpan OwnershipCacheExpiration = TimeSpan.FromMinutes(5);

    public PermissionsHandler(
        IHttpContextAccessor httpContextAccessor,
        HybridCache cache,
        AppDbContext dbContext,
        IUserContext userContext,
        IPermissionService permissionService)
    {
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _dbContext = dbContext;
        _userContext = userContext;
        _permissionService = permissionService;
    }

    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.HasSucceeded)
        {
            return;
        }

        var isAdmin = await CheckIsAdminAsync();
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

    private async Task<bool> CheckIsAdminAsync()
    {
        var userId = _userContext.GetCurrentUserId();
        if (userId == null)
        {
            return false;
        }

        if (!long.TryParse(userId, out var parsedUserId))
        {
            return false;
        }

        var result = await _permissionService.IsUserAdminAsync(parsedUserId);
        return result.IsSuccess && result.Value;
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
        var ownershipPermissions = endpointPermissions.Where(p => p.EndsWith(".owned", StringComparison.OrdinalIgnoreCase)).ToList();
        if (!ownershipPermissions.Any(op => userPermissions.Contains(op)))
        {
            return false; // The user does not have any of the ownership permissions
        }

        var endpoint = httpContext.GetEndpoint();
        var endpointDefinition = endpoint?.Metadata.OfType<FastEndpoints.EndpointDefinition>().FirstOrDefault();
        var entityEndpointAttribute = endpointDefinition?.EndpointAttributes?.OfType<EntityEndpointAttribute>().FirstOrDefault();

        if (entityEndpointAttribute == null)
        {
            throw new InvalidOperationException(
                $"Endpoint '{endpoint?.DisplayName}' has an ownership permission but is missing the [EntityEndpoint] attribute. " +
                "Endpoints with ownership permissions must have the [EntityEndpoint] attribute to specify entity type and ID route parameter.");
        }

        var entityId = httpContext.Request.RouteValues[entityEndpointAttribute.EntityIdRoute]?.ToString();
        if (entityId == null)
        {
            return false; // No entity ID found in route
        }

        var userId = _userContext.GetCurrentUserId();
        if (userId == null)
        {
            return false; // No current user
        }

        var isOwner = await UserOwnsEntityCached(userId, entityEndpointAttribute.EntityType, entityId);
        return isOwner;
    }


    private async Task<bool> UserOwnsEntityCached(string userId, Type entityType, string entityId)
    {
        var cacheKey = $"ownership_{userId}_{entityType.Name}_{entityId}";
        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await UserOwnsEntity(userId, entityType, entityId),
            options: new HybridCacheEntryOptions
            {
                Expiration = OwnershipCacheExpiration
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
            return !string.IsNullOrEmpty(ownedEntity.OwnerId) && ownedEntity.OwnerId.Equals(userId, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

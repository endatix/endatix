using System.Security.Claims;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Attributes;
using Endatix.Infrastructure.Data;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Identity.Authorization.Handlers;

/// <summary>
/// Handles hierarchical authorization rules:
/// - Platform/Tenant admins bypass all permission checks
/// - Enforces entity ownership for ".owned" permissions
/// - Delegates to FastEndpoints' built-in AssertionRequirement for normal permissions/roles/claims and other authorization checks. See more at <see cref="https://github.com/FastEndpoints/FastEndpoints/blob/main/Src/Library/Main/MainExtensions.cs"/>
/// </summary>
public sealed class AssertionPermissionsHandler : AuthorizationHandler<AssertionRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HybridCache _cache;
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserAuthorizationService _authorizationService;

    private static readonly TimeSpan _ownershipCacheExpiration = TimeSpan.FromMinutes(5);

    public AssertionPermissionsHandler(
        IHttpContextAccessor httpContextAccessor,
        HybridCache cache,
        AppDbContext dbContext,
        ICurrentUserAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _dbContext = dbContext;
        _authorizationService = authorizationService;
    }
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AssertionRequirement requirement)
    {
        if (context?.User is not ClaimsPrincipal currentUser)
        {
            return;
        }

        if (await CheckIsAdminAsync(currentUser))
        {
            context.Succeed(requirement);
            return;
        }

        var endpointDefinition = GetEndpointDefinition();
        if (endpointDefinition is null)
        {
            return;
        }

        if (endpointDefinition.AllowedPermissions is not { Count: > 0 } allowedPermissions)
        {
            return;
        }

        var endpointOwnerPermissions = GetEndpointOwnerPermissions(allowedPermissions);
        if (!endpointOwnerPermissions.Any())
        {
            return;
        }

        var userId = context.User?.GetUserId();
        var isOwner = await HandleOwnerRequirementsAsync(endpointDefinition, userId);
        if (isOwner)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail(new AuthorizationFailureReason(this, "User does not have ownership of the entity"));
    }

    /// <summary>
    /// Gets the FastEndpoints definition for the current request
    /// </summary>
    /// <returns>The FastEndpoints definition for the current request, or null for non-FastEndpoints use cases</returns>
    private EndpointDefinition? GetEndpointDefinition()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        var endpoint = httpContext.GetEndpoint();
        var endpointDefinition = endpoint?.Metadata.OfType<FastEndpoints.EndpointDefinition>().FirstOrDefault();
        if (endpointDefinition is null)
        {
            return null;
        }

        return endpointDefinition;
    }

    /// <summary>
    /// Gets the owner permissions for the given endpoint definition.
    /// </summary>
    /// <param name="allowedPermissions">The allowed permissions for the endpoint</param>
    /// <returns>The owner permissions for the given endpoint definition (those that end with ".owned")</returns>
    private IEnumerable<string> GetEndpointOwnerPermissions(IEnumerable<string> allowedPermissions)
    {
        return allowedPermissions.Where(p => p.EndsWith(".owned", StringComparison.OrdinalIgnoreCase));
    }


    /// <summary>
    /// Handles the ownership requirements for the given endpoint definition.
    /// </summary>
    /// <param name="endpointDefinition">The endpoint definition to handle the owner requirements for</param>
    /// <param name="userId">The ID of the user to check the ownership requirements for</param>
    /// <returns>True if the owner requirements are met, false otherwise</returns>
    /// <exception cref="InvalidOperationException">Thrown if the endpoint is missing the [EntityEndpoint] attribute</exception>
    private async Task<bool> HandleOwnerRequirementsAsync(EndpointDefinition endpointDefinition, string? userId)
    {
        Guard.Against.Null(endpointDefinition);
        var entityEndpointAttribute = endpointDefinition.EndpointAttributes?.OfType<EntityEndpointAttribute>().FirstOrDefault();

        if (entityEndpointAttribute is null)
        {
            throw new InvalidOperationException(
                $"Endpoint '{endpointDefinition?.EndpointType.Name}' has an ownership permission but is missing the [EntityEndpoint] attribute. " +
                "Endpoints with ownership permissions must have the [EntityEndpoint] attribute to specify entity type and ID route parameter.");
        }


        var entityId = _httpContextAccessor.HttpContext?.Request.RouteValues[entityEndpointAttribute.EntityIdRoute]?.ToString();
        if (entityId == null)
        {
            return false; // No entity ID found in route
        }

        if (userId is null || string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var isOwner = await UserOwnsEntityCached(userId, entityEndpointAttribute.EntityType, entityId);
        return isOwner;
    }

    /// <summary>
    /// Checks if the current user is an admin using either claims or permission service as a fallback.
    /// </summary>
    /// <param name="principal">The claims principal to check.</param>
    /// <returns>True if the current user is a Tenant or Platform Admin, false otherwise</returns>
    private async Task<bool> CheckIsAdminAsync(ClaimsPrincipal principal)
    {
        var claimCheckResult = principal.IsAdmin();
        if (claimCheckResult.IsSuccess && claimCheckResult.Value)
        {
            return true;
        }

        var userId = principal.GetUserId();
        if (userId is null || !long.TryParse(userId, out var parsedUserId))
        {
            return false;
        }

        var permissionCheckResult = await _authorizationService.IsAdminAsync(CancellationToken.None);
        return permissionCheckResult.IsSuccess && permissionCheckResult.Value;
    }

    private async Task<bool> UserOwnsEntityCached(string userId, Type entityType, string entityId)
    {
        var cacheKey = $"ownership_{userId}_{entityType.Name}_{entityId}";
        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await UserOwnsEntity(userId, entityType, entityId),
            options: new HybridCacheEntryOptions
            {
                Expiration = _ownershipCacheExpiration
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
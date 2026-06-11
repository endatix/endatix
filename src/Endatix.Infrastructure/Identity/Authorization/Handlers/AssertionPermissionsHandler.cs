using System.Security.Claims;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Attributes;
using Endatix.Infrastructure.Data;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

        var isOwner = await HandleOwnerRequirementsAsync(endpointDefinition, currentUser);
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
    /// <param name="currentUser">The current user to check the ownership requirements for</param>
    /// <returns>True if the owner requirements are met, false otherwise</returns>
    /// <exception cref="InvalidOperationException">Thrown if the endpoint is missing the [EntityEndpoint] attribute</exception>
    private async Task<bool> HandleOwnerRequirementsAsync(EndpointDefinition endpointDefinition, ClaimsPrincipal currentUser)
    {
        Guard.Against.Null(endpointDefinition);
        if (endpointDefinition.EndpointAttributes?.OfType<EntityEndpointAttribute>().FirstOrDefault() is not { } entityEndpointAttribute)
        {
            throw new InvalidOperationException(
                $"Endpoint '{endpointDefinition.EndpointType.Name}' has an ownership permission but is missing the [EntityEndpoint] attribute. " +
                "Endpoints with ownership permissions must have the [EntityEndpoint] attribute to specify entity type and ID route parameter.");
        }


        var entityId = _httpContextAccessor.HttpContext?.Request.RouteValues[entityEndpointAttribute.EntityIdRoute]?.ToString();
        if (entityId == null)
        {
            return false; // No entity ID found in route
        }

        var userId = currentUser.GetUserId();
        if (userId is null || string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var isOwner = await UserOwnsEntityCached(currentUser, userId, entityEndpointAttribute.EntityType, entityId);
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

    private async Task<bool> UserOwnsEntityCached(ClaimsPrincipal currentUser, string userId, Type entityType, string entityId)
    {
        var cacheKey = $"ownership_{userId}_{entityType.Name}_{entityId}";
        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel => await UserOwnsEntity(currentUser, userId, entityType, entityId),
            options: new HybridCacheEntryOptions
            {
                Expiration = _ownershipCacheExpiration
            });
    }

    private async Task<bool> UserOwnsEntity(ClaimsPrincipal currentUser, string userId, Type entityType, string entityId)
    {
        if (!typeof(IOwnedEntity).IsAssignableFrom(entityType))
        {
            return false;
        }

        if (!long.TryParse(entityId, out var parsedEntityId))
        {
            return false;
        }

        if (entityType == typeof(Submission))
        {
            var submission = await _dbContext.Submissions
                .AsNoTracking()
                .Include(s => s.Submitter)
                .FirstOrDefaultAsync(s => s.Id == parsedEntityId);

            return submission?.Submitter is not null &&
                UserOwnsSubmitter(currentUser, userId, submission.Submitter);
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

    private static bool UserOwnsSubmitter(ClaimsPrincipal currentUser, string userId, Submitter submitter)
    {
        if (submitter.AppUserId is not null &&
            long.TryParse(userId, out var appUserId) &&
            submitter.AppUserId.Value == appUserId)
        {
            return true;
        }

        var subject = currentUser.FindFirst(ClaimNames.UserId)?.Value;
        return !string.IsNullOrWhiteSpace(subject) &&
            string.Equals(submitter.ExternalSubjectId, subject, StringComparison.Ordinal);
    }
}
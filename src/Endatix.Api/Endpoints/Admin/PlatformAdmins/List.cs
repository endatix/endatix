using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.PlatformAdmin.Common;
using Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformAdmins;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Admin.PlatformAdmins;

/// <summary>
/// Endpoint for listing platform administrators.
/// </summary>
public sealed class List(ListPlatformAdmins listPlatformAdmins)
    : Endpoint<ListPlatformAdminsRequest, Results<Ok<Paged<PlatformAdminUserResponse>>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/admin/platform-admins");
        Policies(AuthorizationPolicies.PlatformAdminAccess);
        Summary(s =>
        {
            s.Summary = "List platform administrators";
            s.Description = "Returns users with a local Endatix PlatformAdmin role assignment.";
            s.Responses[200] = "Platform administrators retrieved successfully.";
            s.Responses[400] = "Invalid request.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<Paged<PlatformAdminUserResponse>>, ProblemHttpResult>> ExecuteAsync(
        ListPlatformAdminsRequest request,
        CancellationToken ct)
    {
        var result = await listPlatformAdmins.ExecuteAsync(
            request.ResolvePage(),
            request.ResolvePageSize(),
            request.Search,
            ct);

        return TypedResultsBuilder
            .MapResult(result, PlatformAdminUserResponse.MapPage)
            .SetTypedResults<Ok<Paged<PlatformAdminUserResponse>>, ProblemHttpResult>();
    }
}

public sealed record ListPlatformAdminsRequest : ISearchablePagedRequest
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? Search { get; set; }
}

/// <summary>
/// Response for a platform administrator user.
/// </summary>
public sealed record PlatformAdminUserResponse
{
    public long Id { get; init; }

    public long TenantId { get; init; }

    public string? TenantName { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? DisplayName { get; init; }

    public string AuthProvider { get; init; } = string.Empty;

    public bool IsExternal { get; init; }

    public bool IsVerified { get; init; }

    public bool IsLockedOut { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    public bool HasExternalPlatformAdminRole { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Maps a <see cref="Paged{PlatformAdminUserListItem}"/> to a <see cref="Paged{PlatformAdminUserResponse}"/>.
    /// </summary>
    public static Paged<PlatformAdminUserResponse> MapPage(Paged<PlatformAdminUserListItem> users)
    {
        IReadOnlyList<PlatformAdminUserResponse> items = users.Items
            .Select(FromListItem)
            .ToList();

        return new Paged<PlatformAdminUserResponse>(
            users.Page,
            users.PageSize,
            users.TotalRecords,
            users.TotalPages,
            items);
    }

    /// <summary>
    /// Maps a <see cref="PlatformAdminUserListItem"/> to a <see cref="PlatformAdminUserResponse"/>.
    /// </summary>
    private static PlatformAdminUserResponse FromListItem(PlatformAdminUserListItem user)
    {
        return new PlatformAdminUserResponse
        {
            Id = user.Id,
            TenantId = user.TenantId,
            TenantName = user.TenantName,
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AuthProvider = user.AuthProvider,
            IsExternal = user.IsExternal,
            IsVerified = user.IsVerified,
            IsLockedOut = user.IsLockedOut,
            LastLoginAt = user.LastLoginAt,
            HasExternalPlatformAdminRole = user.HasExternalPlatformAdminRole,
            Roles = user.Roles
        };
    }
}

/// <summary>
/// Validator for the <see cref="ListPlatformAdminsRequest"/>.
/// </summary>
public sealed class ListPlatformAdminsValidator : Validator<ListPlatformAdminsRequest>
{
    public ListPlatformAdminsValidator()
    {
        Include(new SearchablePagedRequestValidator());
    }
}

using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Tenants.Create;
using Endatix.Infrastructure.Data.Config;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Endpoints.Admin.Tenants;

/// <summary>
/// Endpoint for creating a platform tenant.
/// </summary>
public sealed class Create(IMediator mediator, IConfiguration configuration)
    : Endpoint<CreateTenantRequest, Results<Created<TenantModel>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/admin/tenants");
        Policies(AuthorizationPolicies.PlatformAdminAccess);
        Summary(s =>
        {
            s.Summary = "Create tenant";
            s.Description = "Creates a tenant with its self-registration policy. The slug is immutable after create.";
            s.Responses[201] = "Tenant created successfully.";
            s.Responses[400] = "Invalid input data, or the slug is reserved or already taken.";
            s.Responses[403] = "The current user is not a platform administrator.";
            s.Responses[404] = "Multi-tenancy is not enabled on this deployment.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Created<TenantModel>, ProblemHttpResult>> ExecuteAsync(
        CreateTenantRequest request,
        CancellationToken ct)
    {
        if (!MultiTenancyGate.IsEnabled(configuration))
        {
            return TypedResultsBuilder
                .FromResult(Result<TenantModel>.NotFound(MultiTenancyGate.DisabledMessage))
                .SetTypedResults<Created<TenantModel>, ProblemHttpResult>();
        }

        CreateTenantCommand command = new(
            request.Name!,
            request.Slug!,
            request.Description,
            request.AllowSelfRegistration,
            request.AllowedAuthProviderKeys,
            request.DefaultRegistrationRoleName);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, TenantModel.Map)
            .SetTypedResults<Created<TenantModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request for creating a platform tenant.
/// </summary>
public sealed class CreateTenantRequest
{
    /// <summary>
    /// The tenant display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The requested public tenant slug.
    /// </summary>
    public string? Slug { get; set; }

    /// <summary>
    /// The tenant description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When true, anonymous users may self-register via the tenant slug.
    /// </summary>
    public bool AllowSelfRegistration { get; set; }

    /// <summary>
    /// Host auth provider keys allowed for self-registration.
    /// </summary>
    public List<string>? AllowedAuthProviderKeys { get; set; }

    /// <summary>
    /// The role assigned on self-registration. Omit to use the default.
    /// </summary>
    public string? DefaultRegistrationRoleName { get; set; }
}

/// <summary>
/// Validator for <c>CreateTenantRequest</c>.
/// </summary>
public sealed class CreateTenantValidator : Validator<CreateTenantRequest>
{
    public CreateTenantValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_NAME_LENGTH)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

        RuleFor(request => request.Slug)
            .NotEmpty()
            .ValidUrlSlug();

        RuleFor(request => request.Description)
            .MaximumLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH)
            .When(request => request.Description is not null);

        RuleFor(request => request.DefaultRegistrationRoleName)
            .NotEmpty()
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .When(request => request.DefaultRegistrationRoleName is not null);
    }
}

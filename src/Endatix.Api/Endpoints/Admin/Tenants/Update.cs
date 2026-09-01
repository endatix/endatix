using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Tenants.Update;
using Endatix.Infrastructure.Data.Config;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Endpoints.Admin.Tenants;

/// <summary>
/// Endpoint for partially updating a platform tenant.
/// </summary>
public sealed class Update(IMediator mediator, IConfiguration configuration)
    : Endpoint<UpdateTenantRequest, Results<Ok<TenantModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("/admin/tenants/{tenantId}");
        Policies(AuthorizationPolicies.PlatformAdminAccess);
        Summary(s =>
        {
            s.Summary = "Update tenant";
            s.Description = "Updates the name, description, and self-registration policy of a tenant. The short URL cannot be changed.";
            s.ExampleRequest = new UpdateTenantRequest
            {
                TenantId = 1,
                Name = "Acme",
                AllowSelfRegistration = true
            };
            s.ResponseExamples[200] = TenantModel.Example;
            s.Responses[200] = "Tenant updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[403] = "The current user is not a platform administrator.";
            s.Responses[404] = "Tenant not found, or multi-tenancy is not enabled on this deployment.";
        });
        Description(builder => builder
            .Produces<TenantModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<TenantModel>, ProblemHttpResult>> ExecuteAsync(
        UpdateTenantRequest request,
        CancellationToken ct)
    {
        if (!MultiTenancyGate.IsEnabled(configuration))
        {
            return TypedResultsBuilder
                .FromResult(Result<TenantModel>.NotFound(MultiTenancyGate.DisabledMessage))
                .SetTypedResults<Ok<TenantModel>, ProblemHttpResult>();
        }

        UpdateTenantCommand command = new(
            request.TenantId,
            request.Name,
            request.Description,
            request.AllowSelfRegistration,
            request.AllowedAuthProviderKeys,
            request.DefaultRegistrationRoleName);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, TenantModel.Map)
            .SetTypedResults<Ok<TenantModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request for partially updating a platform tenant. Omitted fields are left unchanged.
/// </summary>
public sealed class UpdateTenantRequest
{
    /// <summary>
    /// The tenant to update.
    /// </summary>
    public long TenantId { get; set; }

    /// <summary>
    /// The new tenant display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The new tenant description. An empty string clears it.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When set, toggles anonymous self-registration via the tenant short URL.
    /// </summary>
    public bool? AllowSelfRegistration { get; set; }

    /// <summary>
    /// When set, replaces the host auth provider keys allowed for self-registration. An empty list clears them.
    /// </summary>
    public List<string>? AllowedAuthProviderKeys { get; set; }

    /// <summary>
    /// When set, the role assigned on self-registration.
    /// </summary>
    public string? DefaultRegistrationRoleName { get; set; }
}

/// <summary>
/// Validator for <c>UpdateTenantRequest</c>.
/// </summary>
public sealed class UpdateTenantValidator : Validator<UpdateTenantRequest>
{
    public UpdateTenantValidator()
    {
        RuleFor(request => request.TenantId)
            .GreaterThan(0);

        RuleFor(request => request)
            .Must(request =>
                request.Name is not null
                || request.Description is not null
                || request.AllowSelfRegistration.HasValue
                || request.AllowedAuthProviderKeys is not null
                || request.DefaultRegistrationRoleName is not null)
            .WithMessage("At least one field must be provided.");

        // Measured after trimming, so the validator and the handler judge the same string.
        RuleFor(request => (request.Name ?? string.Empty).Trim())
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_NAME_LENGTH)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .OverridePropertyName(nameof(UpdateTenantRequest.Name))
            .When(request => request.Name is not null);

        RuleFor(request => request.Description)
            .MaximumLength(DataSchemaConstants.MAX_DESCRIPTION_LENGTH)
            .When(request => request.Description is not null);

        RuleFor(request => request.DefaultRegistrationRoleName)
            .NotEmpty()
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH)
            .When(request => request.DefaultRegistrationRoleName is not null);
    }
}

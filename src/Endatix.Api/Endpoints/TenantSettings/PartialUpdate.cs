using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.TenantSettings.PartialUpdate;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.TenantSettings;

/// <summary>
/// Endpoint for partially updating tenant settings.
/// Note: only supports RequireFolderAssignment setting for now.
/// </summary>
public sealed class PartialUpdate(IMediator mediator)
    : Endpoint<PartialUpdateTenantSettingsRequest, Results<Ok<TenantSettingsModel>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Patch("tenant-settings");
        Permissions(Actions.Tenant.ManageSettings);
        Summary(s =>
        {
            s.Summary = "Partially update tenant settings";
            s.Description = "Updates selected tenant configuration for the current tenant.";
            s.Responses[200] = "Tenant settings updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Tenant settings not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<TenantSettingsModel>, ProblemHttpResult>> ExecuteAsync(
        PartialUpdateTenantSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var updateSettingsCommand = new PartialUpdateTenantSettingsCommand
        {
            RequireFolderAssignment = request.RequireFolderAssignment,
        };
        var updateSettingsResult = await mediator.Send(updateSettingsCommand, cancellationToken);

        return TypedResultsBuilder
            .MapResult(updateSettingsResult, TenantSettingsMapper.Map)
            .SetTypedResults<Ok<TenantSettingsModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request for partially updating tenant settings.
/// </summary>
public sealed class PartialUpdateTenantSettingsRequest
{
    /// <summary>
    /// When set, updates whether forms and templates must be assigned to a folder.
    /// </summary>
    public bool? RequireFolderAssignment { get; set; }
}

/// <summary>
/// Validator for <c>PartialUpdateTenantSettingsRequest</c>.
/// </summary>
public sealed class PartialUpdateTenantSettingsValidator : Validator<PartialUpdateTenantSettingsRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PartialUpdateTenantSettingsValidator()
    {
        RuleFor(x => x.RequireFolderAssignment)
            .NotNull()
            .WithMessage("RequireFolderAssignment must be provided.");
    }
}
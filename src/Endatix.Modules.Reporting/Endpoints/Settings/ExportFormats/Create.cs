using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.ExportFormats;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Settings.ExportFormats;

public sealed class Create(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<CreateExportFormatRequest, Results<Created<ExportFormatDto>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("settings/export-formats");
        Permissions(Actions.Tenant.ManageSettings);
        Summary(static summary =>
        {
            summary.Summary = "Create tenant export format";
            summary.Description = "Creates a new export format configuration for the current tenant.";
            summary.ExampleRequest = new CreateExportFormatRequest
            {
                Name = "CSV Export",
                ExportTarget = ExportTarget.Submissions,
                DeliveryFormat = ExportDeliveryFormat.Csv,
                Profile = ExportProfile.Native,
                Description = "Standard CSV export format"
            };
            summary.ResponseExamples[201] = new ExportFormatDto(
                Id: 1,
                Name: "CSV Export",
                ExportTarget: ExportTarget.Submissions,
                DeliveryFormat: ExportDeliveryFormat.Csv,
                Profile: ExportProfile.Native,
                WireKey: "csv",
                Label: "CSV",
                Description: "Standard CSV export format",
                Settings: new ExportFormatSettings(),
                CreatedAt: DateTime.UtcNow,
                ModifiedAt: null);
            summary.Responses[201] = "Export format created.";
            summary.Responses[400] = "Validation failed.";
        });
        Description(builder => builder
            .Produces<ExportFormatDto>(201, "application/json")
            .ProducesProblem(400));
    }

    public override async Task<Results<Created<ExportFormatDto>, ProblemHttpResult>> ExecuteAsync(
        CreateExportFormatRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateExportFormatCommand(
                tenantContext.TenantId,
                request.Name!,
                request.ExportTarget!.Value,
                request.DeliveryFormat!.Value,
                request.Profile ?? ExportProfile.Native,
                request.Description,
                request.Settings),
            ct);

        return TypedResultsBuilder
            .MapResult(result, format => format)
            .SetTypedResults<Created<ExportFormatDto>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validates the request for the create export format endpoint.
/// </summary>
public sealed class CreateExportFormatEndpointValidator : Validator<CreateExportFormatRequest>
{
    public CreateExportFormatEndpointValidator(IExportCapabilityRegistry capabilityRegistry)
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(ExportFormat.NAME_MAX_LENGTH);

        RuleFor(request => request.Description)
            .MaximumLength(ExportFormat.DESCRIPTION_MAX_LENGTH)
            .When(request => !string.IsNullOrWhiteSpace(request.Description));

        RuleFor(request => request.ExportTarget)
            .NotNull()
            .IsInEnum();

        RuleFor(request => request.DeliveryFormat)
            .NotNull()
            .IsInEnum();

        RuleFor(request => request.Profile)
            .IsInEnum()
            .When(request => request.Profile.HasValue);

        RuleFor(request => request)
            .Must(request =>
                request.ExportTarget.HasValue &&
                request.DeliveryFormat.HasValue &&
                capabilityRegistry.IsValid(
                    request.ExportTarget.Value,
                    request.DeliveryFormat.Value,
                    request.Profile ?? ExportProfile.Native))
            .WithMessage("The selected export target, delivery format, and profile combination is not supported.");
    }
}

/// <summary>
/// The request for the create export format endpoint.
/// </summary>
public sealed class CreateExportFormatRequest
{
    public string? Name { get; init; }

    public ExportTarget? ExportTarget { get; init; }

    public ExportDeliveryFormat? DeliveryFormat { get; init; }

    public ExportProfile? Profile { get; init; }

    public string? Description { get; init; }

    public ExportFormatSettingsInput? Settings { get; init; }
}

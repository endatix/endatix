using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Forms;

/// <summary>
/// Returns discovered SurveyJS locales for a form's compiled reporting schema.
/// </summary>
public sealed class GetFormSchemaLocales(
    IFormSchemaRepository formSchemaRepository,
    ITenantContext tenantContext)
    : Endpoint<GetFormSchemaLocalesRequest, Results<Ok<GetFormSchemaLocalesResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/reporting/locales");
        Permissions(Actions.Submissions.Export, Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "List form reporting locales";
            summary.Description =
                "Returns the locale codes discovered from the form definition and persisted on the reporting FormSchema. " +
                "Use for export label locale selection.";
            summary.Responses[200] = "Locales returned.";
            summary.Responses[404] = "Form schema not found. Compile the schema first.";
            summary.Responses[400] = "Invalid input data.";
        });
        Description(builder => builder
            .Produces<GetFormSchemaLocalesResponse>(200, "application/json")
            .ProducesProblem(404)
            .ProducesProblem(400));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<GetFormSchemaLocalesResponse>, ProblemHttpResult>> ExecuteAsync(
        GetFormSchemaLocalesRequest request,
        CancellationToken ct)
    {
        var schema = await formSchemaRepository.GetByFormIdAsync(
            tenantContext.TenantId,
            request.FormId,
            ct);

        var result = schema is null
            ? Result.NotFound("Form schema not found. Compile the schema first.")
            : Result.Success(
                new GetFormSchemaLocalesResponse
                {
                    FormId = schema.FormId,
                    Locales = FormSchemaLocales.Parse(schema.Locales),
                });

        return TypedResultsBuilder
            .MapResult(result, response => response)
            .SetTypedResults<Ok<GetFormSchemaLocalesResponse>, ProblemHttpResult>();
    }
}

public sealed class GetFormSchemaLocalesValidator : Validator<GetFormSchemaLocalesRequest>
{
    public GetFormSchemaLocalesValidator()
    {
        RuleFor(request => request.FormId)
            .GreaterThan(0);
    }
}

public sealed class GetFormSchemaLocalesRequest
{
    public long FormId { get; init; }
}

public sealed class GetFormSchemaLocalesResponse
{
    public required long FormId { get; init; }

    public required IReadOnlyList<string> Locales { get; init; }
}

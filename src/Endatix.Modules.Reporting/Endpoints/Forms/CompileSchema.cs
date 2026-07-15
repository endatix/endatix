using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.FormSchema;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Forms;

/// <summary>
/// Compiles the reporting form schema for the active form definition.
/// </summary>
public sealed class CompileSchema(
    IMediator mediator,
    ITenantContext tenantContext) : Endpoint<CompileFormSchemaRequest, Results<Ok<CompileFormSchemaResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("forms/{formId}/reporting/compile-schema");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Compile reporting form schema";
            summary.Description =
                "Compiles and persists the export schema for the active form definition. " +
                "Use before exporting when the form predates the reporting pipeline or outbox processing has not run.";
            summary.Responses[200] = "Form schema compiled.";
            summary.Responses[404] = "Form or active definition not found.";
        });
        Description(builder => builder
            .Produces<CompileFormSchemaResponse>(200, "application/json")
            .ProducesProblem(404)
            .ProducesProblem(400));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<CompileFormSchemaResponse>, ProblemHttpResult>> ExecuteAsync(
        CompileFormSchemaRequest request,
        CancellationToken cancellationToken)
    {
        CompileFormSchemaCommand command = new(request.FormId, tenantContext.TenantId);

        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(
                result,
                compiled => new CompileFormSchemaResponse
                {
                    FormId = compiled.FormId,
                    FormDefinitionId = compiled.FormDefinitionId,
                })
            .SetTypedResults<Ok<CompileFormSchemaResponse>, ProblemHttpResult>();
    }
}

public sealed class CompileFormSchemaValidator : Validator<CompileFormSchemaRequest>
{
    public CompileFormSchemaValidator()
    {
        RuleFor(request => request.FormId)
            .GreaterThan(0);
    }
}

/// <summary>
/// Request for the compile schema endpoint.
/// </summary>
public sealed class CompileFormSchemaRequest
{
    public long FormId { get; init; }
}

/// <summary>
/// Response for the compile schema endpoint.
/// </summary>
public sealed class CompileFormSchemaResponse
{
    public long FormId { get; init; }

    public long FormDefinitionId { get; init; }
}

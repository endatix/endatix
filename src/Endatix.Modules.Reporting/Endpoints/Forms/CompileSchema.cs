using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Endpoints.Forms;

/// <summary>
/// Compiles the reporting form schema for the active form definition.
/// </summary>
public sealed class CompileSchema(
    IMediator mediator,
    ITenantContext tenantContext,
    ICurrentUserAuthorizationService authorizationService) : Endpoint<CompileFormSchemaRequest, Results<Ok<CompileFormSchemaResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("forms/{formId}/reporting/compile-schema");
        Permissions(Actions.Submissions.Export, Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Compile reporting form schema";
            summary.Description = "Compiles and persists the export schema for the active form definition. " +
                "Use before exporting when the form predates the reporting pipeline or outbox processing has not run. " +
                "Set replace=true to force Replace mode and clear flattened submissions even when real submissions exist. " +
                "Requires submissions.export or forms.edit; replace=true requires forms.edit.";
            summary.Responses[200] = "Form schema compiled.";
            summary.Responses[403] = "Forbidden. replace=true requires forms.edit; account standing may also deny compilation.";
            summary.Responses[503] = "Account standing temporarily unavailable.";
            summary.Responses[404] = "Form or active definition not found.";
            summary.Responses[409] = "A published reporting alias would change meaning. The previous schema was retained.";
        });
        Description(builder => builder
            .Produces<CompileFormSchemaResponse>(200, "application/json")
            .ProducesProblem(404).ProducesProblem(409)
            .ProducesProblem(403)
            .ProducesProblem(503)
            .ProducesProblem(400));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<CompileFormSchemaResponse>, ProblemHttpResult>> ExecuteAsync(
        CompileFormSchemaRequest request,
        CancellationToken ct)
    {
        var access = await AuthorizeAsync(request, ct);
        if (!access.IsSuccess)
        {
            return access.ToProblem();
        }

        CompileFormSchemaCommand command = new(request.FormId, tenantContext.TenantId, request.Replace);

        var result = await mediator.Send(command, ct);
        if (result.IsConflict())
        {
            return AliasConflict(command, result.Errors.Single());
        }

        return Compiled(result);
    }

    private async Task<Result<bool>> AuthorizeAsync(CompileFormSchemaRequest request, CancellationToken ct)
    {
        if (request.Replace)
        {
            var edit = await authorizationService.ValidateAccessAsync(Actions.Forms.Edit, ct);
            if (!edit.IsSuccess)
            {
                return edit.ToErrorResult<bool>();
            }
        }

        return await mediator.Send(new AuthorizeSchemaCompile(request.FormId, tenantContext.TenantId), ct);
    }

    private static Results<Ok<CompileFormSchemaResponse>, ProblemHttpResult> Compiled(Result<CompileFormSchemaResult> result) =>
        TypedResultsBuilder
            .MapResult(
                result,
                compiled => new CompileFormSchemaResponse
                {
                    FormId = compiled.FormId,
                    FormDefinitionId = compiled.FormDefinitionId,
                })
            .SetTypedResults<Ok<CompileFormSchemaResponse>, ProblemHttpResult>();

    private ProblemHttpResult AliasConflict(CompileFormSchemaCommand command, string detail)
    {
        var problem = EndatixProblemDetails.Create(StatusCodes.Status409Conflict, null, detail, HttpContext, ReportingSchemaAliasConflictException.CODE);
        Logger.LogWarning("Reporting compile rejected for form {FormId}: {ErrorCode}; support reference {TraceId}; {Conflict}",
            command.FormId, ReportingSchemaAliasConflictException.CODE, problem.Extensions["traceId"], detail);
        problem.Extensions["previousSchemaRetained"] = true;
        // Compile has no selected export format, locale or filters and cannot vouch for the download.
        problem.Extensions["previousExportAvailable"] = false;
        return TypedResults.Problem(problem);
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

    /// <summary>
    /// When <c>true</c>, always replace the schema and clear flattened rows.
    /// Defaults to <c>false</c> (auto Merge vs Replace from real submission count).
    /// </summary>
    public bool Replace { get; init; }
}

/// <summary>
/// Response for the compile schema endpoint.
/// </summary>
public sealed class CompileFormSchemaResponse
{
    public long FormId { get; init; }

    public long FormDefinitionId { get; init; }
}

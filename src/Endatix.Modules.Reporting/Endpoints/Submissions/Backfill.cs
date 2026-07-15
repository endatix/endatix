using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Submissions;

/// <summary>
/// Backfills flattened reporting rows for historical submissions on a form.
/// </summary>
public sealed class Backfill(
    IMediator mediator,
    ITenantContext tenantContext) : Endpoint<BackfillSubmissionsRequest, Results<Ok<BackfillSubmissionsResponse>, ProblemHttpResult>>
{
    private const int DefaultBatchSize = 100;

    /// <inheritdoc/>
    public override void Configure()
    {
        Post("forms/{formId}/submissions/backfill");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Backfill flattened submissions";
            summary.Description =
                "Processes a batch of completed submissions into the reporting read model. " +
                "Repeat with nextAfterSubmissionId until hasMore is false. Idempotent unless force is true.";
            summary.Responses[200] = "Backfill batch completed.";
            summary.Responses[400] = "Invalid input data.";
            summary.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<BackfillSubmissionsResponse>(200, "application/json")
            .ProducesProblem(404)
            .ProducesProblem(400));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<BackfillSubmissionsResponse>, ProblemHttpResult>> ExecuteAsync(
        BackfillSubmissionsRequest request,
        CancellationToken ct)
    {
        BackfillSubmissionsCommand command = new(
            FormId: request.FormId,
            TenantId: tenantContext.TenantId,
            BatchSize: request.BatchSize ?? DefaultBatchSize,
            AfterSubmissionId: request.AfterSubmissionId,
            Force: request.Force);

        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, MapResponse)
            .SetTypedResults<Ok<BackfillSubmissionsResponse>, ProblemHttpResult>();
    }

    private static BackfillSubmissionsResponse MapResponse(SubmissionBackfillResult result) =>
        new()
        {
            FormId = result.FormId,
            Scanned = result.Scanned,
            Processed = result.Processed,
            Skipped = result.Skipped,
            Failed = result.Failed,
            HasMore = result.HasMore,
            NextAfterSubmissionId = result.NextAfterSubmissionId,
            FailedSubmissionIds = result.FailedSubmissionIds,
        };
}

/// <summary>
/// Validation rules for <see cref="BackfillSubmissionsRequest"/>.
/// </summary>
public sealed class BackfillSubmissionsValidator : Validator<BackfillSubmissionsRequest>
{
    private const int MaxBatchSize = 500;

    public BackfillSubmissionsValidator()
    {
        RuleFor(request => request.FormId)
            .GreaterThan(0);

        RuleFor(request => request.BatchSize)
            .InclusiveBetween(1, MaxBatchSize)
            .When(request => request.BatchSize.HasValue);

        RuleFor(request => request.AfterSubmissionId)
            .GreaterThan(0)
            .When(request => request.AfterSubmissionId.HasValue);
    }
}

/// <summary>
/// Request to backfill flattened reporting rows for a form.
/// </summary>
public sealed class BackfillSubmissionsRequest
{
    public long FormId { get; init; }

    public int? BatchSize { get; init; }

    public long? AfterSubmissionId { get; init; }

    public bool Force { get; init; }
}

/// <summary>
/// Response from a backfill batch operation.
/// </summary>
public sealed class BackfillSubmissionsResponse
{
    public long FormId { get; init; }

    public int Scanned { get; init; }

    public int Processed { get; init; }

    public int Skipped { get; init; }

    public int Failed { get; init; }

    public bool HasMore { get; init; }

    public long? NextAfterSubmissionId { get; init; }

    public IReadOnlyList<long> FailedSubmissionIds { get; init; } = [];
}

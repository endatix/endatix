using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.SearchCopy;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// Which form's grid columns to read, and in which locale.
/// </summary>
public sealed record SubmissionGridColumnsQuery(long TenantId, long FormId, string Locale);

/// <summary>
/// Reads a form's submissions-grid columns from its compiled schema, with the form's preparation counts.
/// </summary>
public sealed class ListSubmissionGridColumns(
    IFormSchemaRepository formSchemaRepository,
    IFlattenedSubmissionRepository flattenedSubmissionRepository,
    IRepository<Form> formsRepository,
    IRepository<Submission> submissionsRepository)
{

    private static readonly FilterParameters NoFilters = new();
    private static readonly FilterParameters CompletedOnly = new(["isComplete:true"]);
    private static readonly FormSearchKeyStatus NoSearchKeys = new(FormSearchKeys.Empty, OldestRowRevision: null);

    /// <summary>
    /// Returns the columns, or not found when the form does not exist in the tenant. A form without a compiled schema
    /// returns no columns and <see cref="SubmissionGridColumns.SchemaReady"/> <see langword="false"/>.
    /// </summary>
    public async Task<Result<SubmissionGridColumns>> ExecuteAsync(
        SubmissionGridColumnsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.TenantId <= 0)
        {
            return Result.Unauthorized();
        }

        return await IsTenantFormAsync(query, cancellationToken)
            ? Result.Success(await ReadColumnsAsync(query, cancellationToken))
            : Result.NotFound(TenantFormExtensions.FormNotFoundMessage);
    }

    private async Task<SubmissionGridColumns> ReadColumnsAsync(SubmissionGridColumnsQuery query, CancellationToken cancellationToken)
    {
        var schema = await formSchemaRepository.GetByFormIdAsync(query.TenantId, query.FormId, cancellationToken);
        var readiness = await ReadReadinessAsync(query, cancellationToken);
        var search = schema is null
            ? NoSearchKeys
            : await FormSearchKeyStatus.ReadAsync(schema, flattenedSubmissionRepository, cancellationToken);
        var columns = schema is null ? [] : SubmissionGridColumnCatalog.Build(schema, query.Locale, search);

        return new SubmissionGridColumns(query.FormId, schema is not null, readiness, columns, search.Slots);
    }

    private Task<bool> IsTenantFormAsync(SubmissionGridColumnsQuery query, CancellationToken cancellationToken) =>
        formsRepository.IsTenantFormAsync(query.TenantId, query.FormId, cancellationToken);

    private async Task<SubmissionGridReadiness> ReadReadinessAsync(
        SubmissionGridColumnsQuery query,
        CancellationToken cancellationToken)
    {
        var submissions = await submissionsRepository.CountAsync(
            new SubmissionsByFormIdCountSpec(query.FormId, NoFilters), cancellationToken);
        var completed = await submissionsRepository.CountAsync(
            new SubmissionsByFormIdCountSpec(query.FormId, CompletedOnly), cancellationToken);
        var rowsByStatus = await flattenedSubmissionRepository.CountByStatusAsync(
            query.TenantId, query.FormId, cancellationToken);

        return new SubmissionGridReadiness(
            Submissions: submissions,
            CompletedSubmissions: completed,
            Prepared: rowsByStatus.GetValueOrDefault(SubmissionIntegrationStatusCodes.Processed),
            Pending: rowsByStatus.GetValueOrDefault(SubmissionIntegrationStatusCodes.Pending) +
                     rowsByStatus.GetValueOrDefault(SubmissionIntegrationStatusCodes.Processing),
            Failed: rowsByStatus.GetValueOrDefault(SubmissionIntegrationStatusCodes.Failed),
            SkippedInProgress: rowsByStatus.GetValueOrDefault(SubmissionIntegrationStatusCodes.Skipped));
    }
}

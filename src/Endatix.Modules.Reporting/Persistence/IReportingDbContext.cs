using Endatix.Infrastructure.Data.Abstractions;
using Endatix.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// The Reporting export read model, as repositories see it.
/// </summary>
/// <remarks>
/// Runtime code depends on this rather than on a concrete context, so nothing outside
/// <see cref="ReportingPersistence"/> and the module registration branches on the active database provider.
/// </remarks>
public interface IReportingDbContext : ITenantDbContext
{
    DbSet<FormSchema> FormSchemas { get; }

    DbSet<FlattenedSubmission> FlattenedSubmissions { get; }

    DbSet<ExportFormat> ExportFormats { get; }

    DbSet<SurveyTypeExportMapping> SurveyTypeExportMappings { get; }

    /// <summary>
    /// The context's database, which schema rebuilds lock through inside their transaction.
    /// </summary>
    DatabaseFacade Database { get; }

    /// <summary>Lets writes that bypass tracking drop the stale tracked copies of the rows they changed.</summary>
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

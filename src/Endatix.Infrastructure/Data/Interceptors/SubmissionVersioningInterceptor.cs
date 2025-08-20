using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Endatix.Core.Entities;
using Endatix.Core.Abstractions;

namespace Endatix.Infrastructure.Data.Interceptors;

/// <summary>
/// EF Core interceptor that automatically creates submission versions when JsonData changes.
/// This provides automatic data preservation without requiring changes to business logic.
/// </summary>
public class SubmissionVersioningInterceptor : SaveChangesInterceptor
{
    private readonly IIdGenerator<long> _idGenerator;

    public SubmissionVersioningInterceptor(IIdGenerator<long> idGenerator)
    {
        _idGenerator = idGenerator;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not AppDbContext dbContext)
        {
            return result;
        }

        CreateSubmissionVersions(dbContext);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not AppDbContext dbContext)
        {
            return ValueTask.FromResult(result);
        }

        CreateSubmissionVersions(dbContext);
        return ValueTask.FromResult(result);
    }

    private void CreateSubmissionVersions(AppDbContext dbContext)
    {
        var modifiedSubmissions = dbContext.ChangeTracker.Entries<Submission>()
            .Where(e => e.State == EntityState.Modified)
            .ToList();

        foreach (var entry in modifiedSubmissions)
        {
            var submission = entry.Entity;
            
            if (HasJsonDataChanged(entry))
            {
                var version = new SubmissionVersion(
                    submission.TenantId,
                    submission.Id,
                    entry.OriginalValues.GetValue<string>("JsonData") ?? string.Empty
                );

                var versionId = _idGenerator.CreateId();
                typeof(SubmissionVersion).GetProperty("Id")?.SetValue(version, versionId);

                // Set CreatedAt of the version to the effective timestamp of the change
                var effectiveTimestamp = submission.ModifiedAt ?? submission.CreatedAt;
                typeof(SubmissionVersion).GetProperty("CreatedAt")?.SetValue(version, effectiveTimestamp);

                dbContext.SubmissionVersions.Add(version);
            }
        }
    }

    private static bool HasJsonDataChanged(EntityEntry<Submission> entry)
    {
        var originalJsonData = entry.OriginalValues.GetValue<string>("JsonData");
        var currentJsonData = entry.CurrentValues.GetValue<string>("JsonData");

        return !string.Equals(originalJsonData, currentJsonData, StringComparison.Ordinal);
    }
}

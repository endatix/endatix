using System.Diagnostics;
using Endatix.Infrastructure.Data.Logging;
using Microsoft.Extensions.Logging;
using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Endatix.Infrastructure.Data.SeedData;
using Endatix.Infrastructure.Identity.Authentication;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// This class is responsible for seeding the database with sample data. It is designed for creating sample data, but can evolve to support testing cases or SDK templates.
/// </summary>
public class DataSeeder(ILogger<DataSeeder> logger)
{
    /// <summary>
    /// Synchronous version of the seeder, existing because EF Core requires both sync and async seeding methods.
    /// This implementation is safe in the migration/startup context where there's no synchronization context.
    /// For general seeding, prefer using the async version <see cref="SeedSampleDataAsync"/>.
    /// </summary>
    /// <param name="dbContext">The DbContext instance to use for database operations.</param>
    public void SeedSampleData(DbContext dbContext)
    {
        SeedSampleDataAsync(dbContext, CancellationToken.None)
            .Wait();
    }

    /// <summary>
    /// This method seeds the database with sample data for demonstration purposes. It leverages the EF Core <see cref="DbContextOptionsBuilder.UseAsyncSeeding(Func{DbContext, CancellationToken, Task})"/> method for asynchronous seeding. For a comprehensive overview, refer to https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding#configuration-options-useseeding-and-useasyncseeding-methods
    /// </summary>
    /// <param name="dbContext">The DbContext instance to use for database operations.</param>
    /// <param name="cancellationToken">A CancellationToken to observe while executing the operation.</param>
    public async Task SeedSampleDataAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            // A failed Form insert (no definition yet) must not skip retry.
            if (await dbContext.Set<FormDefinition>().AnyAsync(cancellationToken))
            {
                return;
            }

            logger.LogSampleDataSeedingStarted();
            var startTime = Stopwatch.GetTimestamp();

            foreach (var seedData in SeedDataReader.LoadAll())
            {
                await SeedFromFileAsync(dbContext, seedData, cancellationToken);
            }

            var elapsedTime = Stopwatch.GetElapsedTime(startTime);

            logger.LogSampleDataSeeded(elapsedTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogSampleDataSeedingFailed(ex);
            throw;
        }
    }

    private async Task SeedFromFileAsync(DbContext dbContext, FormSeedData seedData, CancellationToken cancellationToken)
    {
        // Forms.ActiveDefinitionId and FormDefinitions.FormId are a cycle: insert Form with
        // null ActiveDefinitionId, then INSERT the definition (must be Added, not Modified),
        // then update the form FK. Pre-stamping a snowflake Id on the definition and only
        // attaching it via the parent makes EF treat it as existing → UPDATE + FK 23503.
        var form = CreateForm(seedData.Form.Name, seedData.Form.Id);
        await dbContext.Set<Form>().AddAsync(form, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var formDefinition = new FormDefinition(tenantId: AuthConstants.DEFAULT_ADMIN_TENANT_ID, jsonData: seedData.Definition.JsonSchema.GetRawText());
        form.AddFormDefinition(formDefinition, isActive: true);
        await dbContext.Set<FormDefinition>().AddAsync(formDefinition, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var submissions = seedData.Submissions
            .Select(sub => CreateSubmission(sub, form))
            .ToList();

        await dbContext.Set<Submission>().AddRangeAsync(submissions, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Detach seeded graph so the next form can be tracked independently.
        dbContext.ChangeTracker.Clear();
    }

    private static Form CreateForm(string name, long? id = null)
    {
        FormCreateArgs args = new(
            TenantId: AuthConstants.DEFAULT_ADMIN_TENANT_ID,
            Name: name,
            IsEnabled: true);
        return id is { } explicitId ? Form.Create(explicitId, args) : Form.Create(args);
    }

    private static Submission CreateSubmission(SubmissionInfo submissionInfo, Form form, long? formDefinitionId = default)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: AuthConstants.DEFAULT_ADMIN_TENANT_ID,
            FormId: form.Id,
            FormDefinitionId: formDefinitionId ?? form.ActiveDefinition!.Id,
            JsonData: submissionInfo.JsonData.GetRawText(),
            IsComplete: submissionInfo.IsComplete));

        submission.UpdateStatus(SubmissionStatus.FromCode(submissionInfo.Status));

        if (submissionInfo.IsComplete)
        {
            var offsetMinutes = Random.Shared.Next(5, 91);
            var completedAt = DateTime.UtcNow.AddMinutes(offsetMinutes);
            typeof(Submission)
                .GetProperty(nameof(Submission.CompletedAt))!
                .SetValue(submission, completedAt);
        }

        // Seed/demo data must not emit webhooks: drop the integration events the aggregate raised
        // (e.g. submission.completed) so they are not captured to the outbox during seeding.
        submission.ClearDomainEvents();

        return submission;
    }
}

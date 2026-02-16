using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data.SeedData;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// This class is responsible for seeding the database with sample data. It is designed for creating sample data, but can evolve to support testing cases or SDK templates.
/// </summary>
public class DataSeeder(ILogger<DataSeeder> logger, IIdGenerator<long> idGenerator)
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
            var hasForms = await dbContext.Set<Form>().AnyAsync(cancellationToken);
            if (hasForms)
            {
                return;
            }

            logger.LogInformation("🌱 Seeding sample data...");
            var startTime = Stopwatch.GetTimestamp();

            foreach (var seedData in SeedDataReader.LoadAll())
            {
                await SeedFromFileAsync(dbContext, seedData, cancellationToken);
            }

            var elapsedTime = Stopwatch.GetElapsedTime(startTime);

            logger.LogInformation("🌱 Sample data seeded. Time taken: {time} ms", elapsedTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "🌱 An error occurred while seeding sample data. Data seeding aborted. Details: {details}", ex.Message);
            throw;
        }
    }

    private async Task SeedFromFileAsync(DbContext dbContext, FormSeedData seedData, CancellationToken cancellationToken)
    {
        var form = CreateForm(seedData.Form.Name, seedData.Form.Id);
        await dbContext.Set<Form>().AddAsync(form, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        CreateDefinition(form, seedData.Definition.JsonSchema.GetRawText());
        await dbContext.SaveChangesAsync(cancellationToken);

        var submissions = seedData.Submissions
            .Select(sub => CreateSubmission(sub, form))
            .ToList();

        await dbContext.Set<Submission>().AddRangeAsync(submissions, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Clear the tracker so shared singleton instances (e.g. SubmissionStatus.New)
        // don't conflict when subsequent forms add their own submissions.
        dbContext.ChangeTracker.Clear();
    }

    private Form CreateForm(string name, long? id = null)
    {
        return new Form(tenantId: 1, name, isEnabled: true)
        {
            Id = id ?? idGenerator.CreateId()
        };
    }

    private FormDefinition CreateDefinition(Form form, string jsonData, long? id = null, bool isActive = true)
    {
        var formDefinition = new FormDefinition(tenantId: 1, jsonData: jsonData)
        {
            Id = id ?? idGenerator.CreateId()
        };

        form.AddFormDefinition(formDefinition, isActive);
        return formDefinition;
    }

    private Submission CreateSubmission(SubmissionInfo submissionInfo, Form form, long? formDefinitionId = default)
    {
        var submission = new Submission(tenantId: 1, submissionInfo.JsonData.GetRawText(), form.Id,
            formDefinitionId ?? form.ActiveDefinition!.Id, submissionInfo.IsComplete)
        {
            Id = idGenerator.CreateId()
        };

        // Use 'with {}' to create a fresh owned entity instance per submission.
        // Sharing the same static SubmissionStatus singleton across multiple Submission
        // objects causes EF Core to omit the Status column from some INSERTs.
        var status = submissionInfo.Status switch
        {
            "read" => SubmissionStatus.Read with {},
            "approved" => SubmissionStatus.Approved with {},
            "declined" => SubmissionStatus.Declined with {},
            _ => SubmissionStatus.New with {}
        };
        submission.UpdateStatus(status);

        if (submissionInfo.IsComplete)
        {
            var offsetMinutes = Random.Shared.Next(5, 91);
            var completedAt = DateTime.UtcNow.AddMinutes(offsetMinutes);
            typeof(Submission)
                .GetProperty(nameof(Submission.CompletedAt))!
                .SetValue(submission, completedAt);
        }

        return submission;
    }
}

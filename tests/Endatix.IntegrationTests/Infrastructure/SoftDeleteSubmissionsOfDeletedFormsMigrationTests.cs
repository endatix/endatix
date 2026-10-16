using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The data migration that soft-deletes submissions left live under a deleted form, run as the active provider's
/// migration (its own SQL on each provider) against rows an earlier version could have left.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class SoftDeleteSubmissionsOfDeletedFormsMigrationTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string MigrationName = "SoftDeleteSubmissionsOfDeletedForms";

    private static readonly DateTime EarlierDeletion = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public async Task Migrating_soft_deletes_only_the_live_submissions_of_deleted_forms()
    {
        // Arrange
        var (services, seeded) = await SeedAsync();
        var before = DateTime.UtcNow;

        // Act
        await MigrateAsync(services);

        // Assert
        var leftover = await RowAsync(services, seeded.Leftover);
        Assert.True(leftover.IsDeleted);
        Assert.True(leftover.DeletedAt >= before.AddMinutes(-1), $"DeletedAt {leftover.DeletedAt} is not the migration's.");
        Assert.Equal(leftover.DeletedAt, leftover.ModifiedAt);
        var earlier = await RowAsync(services, seeded.DeletedEarlier);
        Assert.True(earlier.IsDeleted);
        Assert.Equal(EarlierDeletion, earlier.DeletedAt);
        Assert.False((await RowAsync(services, seeded.OfLiveForm)).IsDeleted);
    }

    [Fact]
    public async Task Migrating_again_changes_nothing()
    {
        // Arrange
        var (services, seeded) = await SeedAsync();
        await MigrateAsync(services);
        var first = await RowAsync(services, seeded.Leftover);

        // Act
        await MigrateAsync(services);

        // Assert
        Assert.Equal(first, await RowAsync(services, seeded.Leftover));
        Assert.False((await RowAsync(services, seeded.OfLiveForm)).IsDeleted);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Seeded(long Leftover, long DeletedEarlier, long OfLiveForm);

    private sealed record Row(bool IsDeleted, DateTime? DeletedAt, DateTime? ModifiedAt);

    /// <summary>
    /// A deleted form, as an earlier version could leave it: its row deleted, its definition and one submission live,
    /// and one submission deleted before it. Plus a live form with a live submission.
    /// </summary>
    private async Task<(IServiceProvider Services, Seeded Seeded)> SeedAsync()
    {
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" },
            Ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Ct);
        var deletedForm = await CreateFormAsync(admin);
        var leftover = await CreateSubmissionAsync(admin, deletedForm);
        var deletedEarlier = await CreateSubmissionAsync(admin, deletedForm);
        using var removed = await admin.DeleteAsync($"/api/forms/{deletedForm}/submissions/{deletedEarlier}", Ct);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var liveForm = await CreateFormAsync(admin);
        var ofLiveForm = await CreateSubmissionAsync(admin, liveForm);
        await LeaveAsAnEarlierVersionWouldAsync(world.Services, deletedForm, deletedEarlier);
        return (world.Services, new Seeded(leftover, deletedEarlier, ofLiveForm));
    }

    private static async Task LeaveAsAnEarlierVersionWouldAsync(IServiceProvider services, long formId, long deletedEarlier)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await app.Forms.IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
            .Where(form => form.Id == formId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(form => form.IsDeleted, true), Ct);
        await AllSubmissions(app)
            .Where(row => row.Id == deletedEarlier)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.DeletedAt, EarlierDeletion), Ct);
    }

    /// <summary>The active provider's migration, its SQL run as the migrator would run it.</summary>
    private static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrations = app.GetService<IMigrationsAssembly>();
        var id = migrations.Migrations.Keys.Single(key => key.EndsWith($"_{MigrationName}", StringComparison.Ordinal));
        var migration = migrations.CreateMigration(migrations.Migrations[id], app.Database.ProviderName!);
        foreach (var operation in migration.UpOperations.OfType<SqlOperation>())
        {
            await app.Database.ExecuteSqlRawAsync(operation.Sql, Ct);
        }
    }

    private static async Task<Row> RowAsync(IServiceProvider services, long submissionId)
    {
        await using var scope = services.CreateAsyncScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await AllSubmissions(app).AsNoTracking()
            .Where(row => row.Id == submissionId)
            .Select(row => new Row(row.IsDeleted, row.DeletedAt, row.ModifiedAt))
            .SingleAsync(Ct);
    }

    private static IQueryable<Submission> AllSubmissions(AppDbContext app) =>
        app.Submissions.IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete, EndatixQueryFilterNames.Tenant]);

    private static async Task<long> CreateFormAsync(HttpClient admin)
    {
        using var response = await admin.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"migration-form-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q"}]}]}""",
            },
            Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await IdAsync(response);
    }

    private static async Task<long> CreateSubmissionAsync(HttpClient admin, long formId)
    {
        using var response = await admin.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = false, jsonData = "{}", currentPage = 0 },
            Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await IdAsync(response);
    }

    private static async Task<long> IdAsync(HttpResponseMessage response) =>
        long.Parse((await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString()!);
}

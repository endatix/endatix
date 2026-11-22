using System.Text.Json.Nodes;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// The migration that copies every form's webhook settings from <c>Forms.WebHookSettingsJson</c> into the webhooks
/// section of its settings, on PostgreSQL's jsonb and on SQL Server's native json column.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P0")]
public sealed class MoveFormWebHookSettingsMigrationTests(DbIntegrationFixture fixture)
{
    private const long TENANT_ID = 92;
    private const string MIGRATION_NAME = "MoveFormWebHookSettings";

    // As stored by Form.UpdateWebHookSettings, and in another casing, as an older client could have stored it.
    private const string PASCAL_CASE = """{"Events":{"SubmissionCompleted":{"IsEnabled":true,"WebHookEndpoints":[{"Url":"https://hooks.example.test/a"}]}}}""";
    private const string CAMEL_CASE = """{"events":{"submissioncompleted":{"isEnabled":true,"webHookEndpoints":[{"url":"https://hooks.example.test/b"}]}}}""";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrate_Up_CopiesEachFormsWebHooksAsStoredAndLeavesFormsWithoutThemAlone()
    {
        // Arrange: rows are seeded on the current schema, then the database goes back to before the copy
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, Cancellation);
        var services = IntegrationCoreMigrationTestHelper.BuildServiceProvider(fixture.ConnectionString, fixture.Provider);
        await services.ApplyDbMigrationsAsync(NullLogger.Instance, Cancellation);
        using var scope = services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<AppDbContext>().GetService<IMigrator>();
        var (previous, move) = MigrationIds(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        var seeded = await SeedAsync();
        await migrator.MigrateAsync(previous, Cancellation);

        // Act
        await migrator.MigrateAsync(move, Cancellation);

        // Assert
        var rows = await ReadSettingsAsync();
        AssertCopied(rows[seeded.NoRow], PASCAL_CASE, version: 1);
        AssertCopied(rows[seeded.CamelCase], CAMEL_CASE, version: 1);
        AssertCopied(rows[seeded.Deleted], PASCAL_CASE, version: 1);
        AssertCopied(rows[seeded.WithPdfRow], PASCAL_CASE, version: 2);
        rows[seeded.WithPdfRow].PdfJson.Should().NotBeNull("the copy keeps the other sections");
        rows.Should().NotContainKey(seeded.NoWebHooks);
        rows.Should().NotContainKey(seeded.JsonNull);
        rows[seeded.CamelCase].WebHooks.Events.Should().ContainKey("submissioncompleted");
    }

    [Fact]
    public async Task Migrate_UpDownUp_KeepsTheFormsColumnAndTheAddedRowsAndCopiesAgain()
    {
        // Arrange
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, Cancellation);
        var services = IntegrationCoreMigrationTestHelper.BuildServiceProvider(fixture.ConnectionString, fixture.Provider);
        await services.ApplyDbMigrationsAsync(NullLogger.Instance, Cancellation);
        using var scope = services.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<AppDbContext>().GetService<IMigrator>();
        var (previous, move) = MigrationIds(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        var seeded = await SeedAsync();
        await migrator.MigrateAsync(previous, Cancellation);
        await migrator.MigrateAsync(move, Cancellation);

        // Act
        await migrator.MigrateAsync(previous, Cancellation);
        var column = await ReadFormColumnAsync(seeded.NoRow);
        var rowsAfterDown = await ReadSettingsRowIdsAsync();
        await migrator.MigrateAsync(move, Cancellation);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(column!), JsonNode.Parse(PASCAL_CASE)).Should().BeTrue("Down never touches the form");
        rowsAfterDown.Should().Contain(seeded.NoRow, "Down drops the section, not the rows the copy added");
        AssertCopied((await ReadSettingsAsync())[seeded.NoRow], PASCAL_CASE, version: 2);
    }

    // Only the key: before the migration the model's WebHooksJson column does not exist.
    private async Task<List<long>> ReadSettingsRowIdsAsync()
    {
        await using var db = CreateAppDbContext();
        return await db.FormSettings.IgnoreQueryFilters().Select(settings => settings.FormId).ToListAsync(Cancellation);
    }

    private static void AssertCopied(FormSettings settings, string expected, long version)
    {
        JsonNode.DeepEquals(JsonNode.Parse(settings.WebHooksJson!), JsonNode.Parse(expected))
            .Should().BeTrue(settings.WebHooksJson);
        settings.Version.Should().Be(version);
        settings.TenantId.Should().Be(TENANT_ID);
        settings.ModifiedAt.Should().NotBeNull();
    }

    // Through the current model, with the mirror out of the way: the columns are written as data, as before the copy.
    private async Task<SeededForms> SeedAsync()
    {
        await using var db = CreateAppDbContext();
        db.Set<Tenant>().Add(new Tenant("webhook-settings-move", "tnntwk92") { Id = TENANT_ID });
        await db.SaveChangesAsync(Cancellation);
        var forms = Enumerable.Range(0, 6).Select(index => Form.Create(new FormCreateArgs(TenantId: TENANT_ID, Name: $"form-{index}"))).ToList();
        db.Forms.AddRange(forms);
        await db.SaveChangesAsync(Cancellation);
        var seeded = new SeededForms(forms[0].Id, forms[1].Id, forms[2].Id, forms[3].Id, forms[4].Id, forms[5].Id);

        var withPdf = FormSettings.For(forms[3]);
        withPdf.Apply(new FormSettingsChange { Pdf = new PdfSettings { UsesFormTheme = true } }, FormSettings.UNSAVED_VERSION, DateTime.UtcNow);
        db.FormSettings.Add(withPdf);
        await db.SaveChangesAsync(Cancellation);

        await StoreAsync(seeded.NoRow, PASCAL_CASE);
        await StoreAsync(seeded.CamelCase, CAMEL_CASE);
        await StoreAsync(seeded.Deleted, PASCAL_CASE);
        await StoreAsync(seeded.WithPdfRow, PASCAL_CASE);
        if (fixture.Provider == TestDatabaseProvider.PostgreSql)
        {
            await StoreAsync(seeded.JsonNull, "null"); // SQL Server's json type holds only an object or an array
        }

        await db.Forms.Where(form => form.Id == seeded.Deleted)
            .ExecuteUpdateAsync(update => update.SetProperty(form => form.IsDeleted, true), Cancellation);
        return seeded;

        Task<int> StoreAsync(long formId, string json) => db.Forms.Where(form => form.Id == formId)
            .ExecuteUpdateAsync(update => update.SetProperty(form => form.WebHookSettingsJson, json), Cancellation);
    }

    private async Task<Dictionary<long, FormSettings>> ReadSettingsAsync()
    {
        await using var db = CreateAppDbContext();
        return await db.FormSettings.IgnoreQueryFilters().AsNoTracking()
            .Where(settings => settings.TenantId == TENANT_ID)
            .ToDictionaryAsync(settings => settings.FormId, Cancellation);
    }

    private async Task<string?> ReadFormColumnAsync(long formId)
    {
        await using var db = CreateAppDbContext();
        return await db.Forms.IgnoreQueryFilters().AsNoTracking()
            .Where(form => form.Id == formId)
            .Select(form => form.WebHookSettingsJson)
            .SingleAsync(Cancellation);
    }

    private static (string Previous, string Move) MigrationIds(AppDbContext db)
    {
        var all = db.Database.GetMigrations().ToList();
        var index = all.FindIndex(id => id.EndsWith("_" + MIGRATION_NAME, StringComparison.Ordinal));
        index.Should().BePositive();
        return (all[index - 1], all[index]);
    }

    private AppDbContext CreateAppDbContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigureOptions(optionsBuilder, fixture.ConnectionString, fixture.Provider);
        return new AppDbContext(optionsBuilder.Options, new IntegrationTenantContext(TENANT_ID), new OutboxIntegrationEventDispatcher());
    }

    private sealed record SeededForms(long NoRow, long CamelCase, long Deleted, long WithPdfRow, long NoWebHooks, long JsonNull);
}

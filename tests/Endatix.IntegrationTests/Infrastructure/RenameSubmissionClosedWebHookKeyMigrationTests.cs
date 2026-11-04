using System.Text.Json;
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
/// The migration that renames the stored webhook setting <c>SubmissionCollectionStatusChanged</c> to
/// <c>SubmissionClosed</c>, on PostgreSQL's jsonb and on SQL Server's native json column, up and back down.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P0")]
public sealed class RenameSubmissionClosedWebHookKeyMigrationTests(DbIntegrationFixture fixture)
{
    private const long TenantId = 91;
    private const string MigrationName = "RenameSubmissionClosedWebHookKey";
    private const string OldKey = "SubmissionCollectionStatusChanged";
    private const string NewKey = "SubmissionClosed";
    private const string ScreenOutUrl = "https://hooks.example.test/screen-out";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrate_UpThenDown_MovesTheKeyKeepingItsSettingAndLeavesOtherDocumentsAlone()
    {
        // Arrange
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, Cancellation);
        var services = IntegrationCoreMigrationTestHelper.BuildServiceProvider(fixture.ConnectionString, fixture.Provider);
        await services.ApplyDbMigrationsAsync(NullLogger.Instance, Cancellation);
        using var scope = services.CreateScope();
        var migrationDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrator = migrationDb.GetService<IMigrator>();
        var (previous, rename) = MigrationIds(migrationDb);
        await migrator.MigrateAsync(previous, Cancellation);
        var seeded = await SeedAsync();

        // Act
        await migrator.MigrateAsync(rename, Cancellation);
        var up = await ReadAsync(seeded);
        await migrator.MigrateAsync(previous, Cancellation);
        var down = await ReadAsync(seeded);
        await migrator.MigrateAsync(rename, Cancellation);

        // Assert
        AssertMoved(up.OldKeyForm, from: OldKey, to: NewKey);
        AssertMoved(up.Tenant, from: OldKey, to: NewKey);
        EventKeys(up.BothKeysForm).Should().Contain(NewKey).And.NotContain(OldKey, "the new key wins and the old one is dropped");
        up.NoWebHooksForm.Should().BeNull();
        AssertMoved(down.OldKeyForm, from: NewKey, to: OldKey);
        AssertMoved(down.Tenant, from: NewKey, to: OldKey);
    }

    private static (string Previous, string Rename) MigrationIds(AppDbContext db)
    {
        var all = db.Database.GetMigrations().ToList();
        var index = all.FindIndex(id => id.EndsWith("_" + MigrationName, StringComparison.Ordinal));
        index.Should().BePositive();
        return (all[index - 1], all[index]);
    }

    private static void AssertMoved(string? json, string from, string to)
    {
        using var document = JsonDocument.Parse(json!);
        var events = document.RootElement.GetProperty("Events");
        events.TryGetProperty(from, out _).Should().BeFalse();
        events.TryGetProperty("SubmissionCompleted", out _).Should().BeTrue("other settings stay");
        var moved = events.GetProperty(to);
        moved.GetProperty("IsEnabled").GetBoolean().Should().BeTrue();
        moved.GetProperty("WebHookEndpoints")[0].GetProperty("Url").GetString().Should().Be(ScreenOutUrl);
    }

    private static IEnumerable<string> EventKeys(string? json)
    {
        using var document = JsonDocument.Parse(json!);
        return [.. document.RootElement.GetProperty("Events").EnumerateObject().Select(property => property.Name)];
    }

    private static string Settings(params string[] screenOutKeys) => JsonSerializer.Serialize(new WebHookConfiguration
    {
        Events = new Dictionary<string, WebHookEventConfig>(screenOutKeys.Select(key => KeyValuePair.Create(key, ScreenOut())))
        {
            ["SubmissionCompleted"] = new() { IsEnabled = false, WebHookEndpoints = [] },
        },
    });

    private static WebHookEventConfig ScreenOut() =>
        new() { IsEnabled = true, WebHookEndpoints = [new WebHookEndpointConfig { Url = ScreenOutUrl }] };

    private async Task<SeededRows> SeedAsync()
    {
        await using var db = CreateAppDbContext();
        db.Set<Tenant>().Add(new Tenant("webhook-key-rename", "tnntwk91") { Id = TenantId });
        await db.SaveChangesAsync(Cancellation);
        db.Set<TenantSettings>().Add(new TenantSettings(TenantId, webHookSettingsJson: Settings(OldKey)));
        var oldKey = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "old-key", WebHookSettingsJson: Settings(OldKey)));
        var bothKeys = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "both-keys", WebHookSettingsJson: Settings(OldKey, NewKey)));
        var noWebHooks = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "no-webhooks"));
        db.Forms.AddRange(oldKey, bothKeys, noWebHooks);
        await db.SaveChangesAsync(Cancellation);
        return new SeededRows(oldKey.Id, bothKeys.Id, noWebHooks.Id);
    }

    private async Task<StoredSettings> ReadAsync(SeededRows rows)
    {
        await using var db = CreateAppDbContext();
        var forms = await db.Forms.IgnoreQueryFilters().AsNoTracking()
            .Where(form => form.TenantId == TenantId)
            .ToDictionaryAsync(form => form.Id, form => form.WebHookSettingsJson, Cancellation);
        var tenant = await db.Set<TenantSettings>().IgnoreQueryFilters().AsNoTracking()
            .Where(settings => settings.TenantId == TenantId)
            .Select(settings => settings.WebHookSettingsJson)
            .SingleAsync(Cancellation);
        return new StoredSettings(forms[rows.OldKey], forms[rows.BothKeys], forms[rows.NoWebHooks], tenant);
    }

    private AppDbContext CreateAppDbContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigureOptions(optionsBuilder, fixture.ConnectionString, fixture.Provider);
        return new AppDbContext(optionsBuilder.Options, new IntegrationTenantContext(TenantId), new OutboxIntegrationEventDispatcher());
    }

    private sealed record SeededRows(long OldKey, long BothKeys, long NoWebHooks);

    private sealed record StoredSettings(string? OldKeyForm, string? BothKeysForm, string? NoWebHooksForm, string? Tenant);
}

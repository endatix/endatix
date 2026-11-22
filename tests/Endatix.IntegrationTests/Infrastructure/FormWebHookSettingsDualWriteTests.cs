using Endatix.Core.Entities;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormSettings;
using Endatix.Core.UseCases.FormSettings.PartialUpdate;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Repositories;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

/// <summary>
/// While <c>Forms.WebHookSettingsJson</c> stays (endatix/endatix#1784), a form's webhook settings are written to it and
/// to the settings' webhooks section in the same save, whichever side a write changes, so the previous release still
/// reads them after a rollback.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P0")]
public sealed class FormWebHookSettingsDualWriteTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string HOOK_URL = "https://hooks.example.test/completed";
    private const string OTHER_HOOK_URL = "https://hooks.example.test/other";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_new_form_with_webhooks_gets_the_section_in_the_same_save()
    {
        // Arrange
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: Name(), WebHookSettingsJson: Config(HOOK_URL).Json()));

        // Act
        await using (var db = CreateDb(tenantId))
        {
            db.Forms.Add(form);
            await db.SaveChangesAsync(Cancellation);
        }

        // Assert
        var settings = await ReadSettingsAsync(tenantId, form.Id);
        settings!.Version.Should().Be(1);
        settings.WebHooks.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url.Should().Be(HOOK_URL);
    }

    [Fact]
    public async Task A_new_form_without_webhooks_gets_no_settings_row()
    {
        // Arrange
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);

        // Act
        var formId = await InsertFormAsync(tenantId, webHooks: null);

        // Assert
        (await ReadSettingsAsync(tenantId, formId)).Should().BeNull();
    }

    [Fact]
    public async Task Changing_and_clearing_a_forms_webhooks_changes_and_clears_the_section()
    {
        // Arrange
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        var formId = await InsertFormAsync(tenantId, Config(HOOK_URL));

        // Act
        await UpdateFormAsync(tenantId, formId, Config(OTHER_HOOK_URL));
        var changed = await ReadSettingsAsync(tenantId, formId);
        await UpdateFormAsync(tenantId, formId, null);
        var cleared = await ReadSettingsAsync(tenantId, formId);

        // Assert
        changed!.Version.Should().Be(2);
        changed.WebHooks.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url.Should().Be(OTHER_HOOK_URL);
        cleared!.Version.Should().Be(3);
        cleared.WebHooksJson.Should().BeNull("a form without webhooks of its own uses its tenant's");
    }

    [Fact]
    public async Task A_settings_write_of_webhooks_writes_the_form_in_the_shape_the_previous_release_reads()
    {
        // Arrange
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        var formId = await InsertFormAsync(tenantId, webHooks: null);

        // Act
        var result = await UpdateSettingsAsync((tenantId, formId), new FormSettingsChange { WebHooks = Config(HOOK_URL) }, 0);

        // Assert
        result.Value.Version.Should().Be(1);
        var stored = await ReadFormAsync(tenantId, formId);
        stored.WebHookSettingsJson.Should().Contain("\"Events\"").And.Contain("\"WebHookEndpoints\"");
        stored.WebHookSettings.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url.Should().Be(HOOK_URL);
    }

    [Fact]
    public async Task A_pdf_only_first_write_leaves_the_forms_webhooks_alone()
    {
        // Arrange: a form an older instance wrote during a rolling deploy, with webhooks and no settings row
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        var formId = await InsertFormAsync(tenantId, Config(HOOK_URL));
        await using (var db = CreateDb(tenantId))
        {
            await db.FormSettings.Where(settings => settings.FormId == formId).ExecuteDeleteAsync(Cancellation);
        }

        // Act
        var result = await UpdateSettingsAsync((tenantId, formId), new FormSettingsChange { Pdf = new PdfSettings { UsesFormTheme = true } }, 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await ReadFormAsync(tenantId, formId)).WebHookSettings.Events.Should().ContainKey("SubmissionCompleted");
    }

    [Fact]
    public async Task A_conceded_webhooks_write_changes_neither_side_and_leaves_nothing_tracked()
    {
        // Arrange: two requests read version 1; one stores other webhooks first
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        var formId = await InsertFormAsync(tenantId, Config(HOOK_URL));
        await using var slowDb = CreateDb(tenantId);
        await slowDb.Forms.SingleAsync(form => form.Id == formId, Cancellation);
        var slow = (await Repository(slowDb).FindAsync(formId, Cancellation))!;
        await UpdateSettingsAsync((tenantId, formId), new FormSettingsChange { WebHooks = Config(OTHER_HOOK_URL) }, 1);
        slow.Apply(new FormSettingsChange { WebHooks = new WebHookConfiguration() }, 1, DateTime.UtcNow).Value.Should().BeTrue();

        // Act
        var saved = await Repository(slowDb).SaveOrConcedeAsync(slow, Cancellation);

        // Assert
        saved.Should().BeFalse();
        slowDb.ChangeTracker.Entries().Should().BeEmpty("the form the refused write changed must not be saved later");
        (await ReadFormAsync(tenantId, formId)).WebHookSettings.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url
            .Should().Be(OTHER_HOOK_URL);
    }

    [Fact]
    public async Task A_new_form_saved_synchronously_gets_the_section_too()
    {
        // Arrange
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: Name(), WebHookSettingsJson: Config(HOOK_URL).Json()));

        // Act
        await using (var db = CreateDb(tenantId))
        {
            db.Forms.Add(form);
            db.SaveChanges();
        }

        // Assert
        (await ReadSettingsAsync(tenantId, form.Id))!.WebHooks.Events["SubmissionCompleted"].WebHookEndpoints.Single().Url
            .Should().Be(HOOK_URL);
    }

    [Fact]
    public async Task A_whole_form_update_after_a_settings_write_leaves_the_settings_version_alone()
    {
        // Arrange: the section is stored camelCase and the column Pascal case; a repository update marks every column
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        var formId = await InsertFormAsync(tenantId, webHooks: null);
        await UpdateSettingsAsync((tenantId, formId), new FormSettingsChange { WebHooks = Config(HOOK_URL) }, 0);

        // Act
        await using (var db = CreateDb(tenantId))
        {
            var forms = new EfRepository<Form>(db, new EndatixSpecificationEvaluator([]));
            var form = (await forms.GetByIdAsync(formId, Cancellation))!;
            await forms.UpdateAsync(form, Cancellation);
        }

        var again = await UpdateSettingsAsync((tenantId, formId), new FormSettingsChange { WebHooks = Config(HOOK_URL) }, 0);

        // Assert
        (await ReadSettingsAsync(tenantId, formId))!.Version.Should().Be(1);
        again.IsSuccess.Should().BeTrue("the same settings again are a no-op whatever the version");
        again.Value.Version.Should().Be(1);
        again.Value.WebHooks!.Events.Should().ContainKey("SubmissionCompleted");
    }

    private async Task<long> InsertFormAsync(long tenantId, WebHookConfiguration? webHooks)
    {
        await using var db = CreateDb(tenantId);
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: Name(), WebHookSettingsJson: webHooks?.Json()));
        db.Forms.Add(form);
        await db.SaveChangesAsync(Cancellation);
        return form.Id;
    }

    private async Task UpdateFormAsync(long tenantId, long formId, WebHookConfiguration? webHooks)
    {
        await using var db = CreateDb(tenantId);
        var form = await db.Forms.SingleAsync(candidate => candidate.Id == formId, Cancellation);
        form.UpdateWebHookSettings(webHooks);
        await db.SaveChangesAsync(Cancellation);
    }

    private async Task<Result<FormSettingsDto>> UpdateSettingsAsync((long TenantId, long FormId) form, FormSettingsChange change, long version)
    {
        var (tenantId, formId) = form;
        await using var db = CreateDb(tenantId);
        return await new PartialUpdateFormSettingsHandler(new EfRepository<Form>(db, new EndatixSpecificationEvaluator([])), Repository(db), new DateTimeProvider())
            .Handle(new PartialUpdateFormSettingsCommand(formId, version, change), Cancellation);
    }

    private async Task<FormSettings?> ReadSettingsAsync(long tenantId, long formId)
    {
        await using var db = CreateDb(tenantId);
        return await db.FormSettings.AsNoTracking().SingleOrDefaultAsync(settings => settings.FormId == formId, Cancellation);
    }

    private async Task<Form> ReadFormAsync(long tenantId, long formId)
    {
        await using var db = CreateDb(tenantId);
        return await db.Forms.AsNoTracking().SingleAsync(form => form.Id == formId, Cancellation);
    }

    private static string Name() => $"webhooks-{Guid.NewGuid():N}";

    private static WebHookConfiguration Config(string url) => new()
    {
        Events = new Dictionary<string, WebHookEventConfig>
        {
            ["SubmissionCompleted"] = new() { IsEnabled = true, WebHookEndpoints = [new WebHookEndpointConfig { Url = url }] },
        },
    };

    private static FormSettingsRepository Repository(AppDbContext db) => new(db, new UniqueConstraintViolationChecker());

    private AppDbContext CreateDb(long tenantId)
    {
        DbContextOptionsBuilder<AppDbContext> options = new();
        IntegrationAppDbContextFactory.ConfigureOptions(options, fixture.Database.ConnectionString, fixture.Database.Provider);
        return new AppDbContext(options.Options, new IntegrationTenantContext(tenantId), new OutboxIntegrationEventDispatcher());
    }
}

internal static class WebHookConfigurationJson
{
    public static string Json(this WebHookConfiguration config) => System.Text.Json.JsonSerializer.Serialize(config);
}

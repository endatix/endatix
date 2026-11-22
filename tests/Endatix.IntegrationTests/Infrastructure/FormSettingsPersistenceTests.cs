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
/// A form's settings against the real database: the JSON column (jsonb on PostgreSQL) keeps a retried write a no-op
/// after a reload, stored JSON with unknown fields reads back, and the version token and the key turn a lost race into
/// a conflict.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class FormSettingsPersistenceTests(EndatixIntegrationWebHostFixture fixture)
{
    private static readonly FormSettingsChange _themeOn = new() { Pdf = new PdfSettings { UsesFormTheme = true } };
    private static readonly FormSettingsChange _themeOff = new() { Pdf = new PdfSettings() };

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_write_round_trips_and_its_retry_after_a_reload_changes_nothing()
    {
        // Arrange
        var (tenantId, formId) = await InsertFormAsync();

        // Act
        var first = await UpdateAsync((tenantId, formId), _themeOn, 0);
        var retry = await UpdateAsync((tenantId, formId), _themeOn, 0);

        // Assert
        first.Value.Version.Should().Be(1);
        retry.IsSuccess.Should().BeTrue();
        retry.Value.Version.Should().Be(1);
        var stored = await ReadAsync(tenantId, formId);
        stored.Version.Should().Be(1);
        stored.Pdf.UsesFormTheme.Should().BeTrue();
        stored.ModifiedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("{}", false)]
    [InlineData("""{"usesFormTheme":true,"future":1}""", true)]
    public async Task Stored_json_with_missing_or_unknown_fields_reads_back(string stored, bool usesFormTheme)
    {
        // Arrange
        var (tenantId, formId) = await InsertFormAsync();
        await UpdateAsync((tenantId, formId), _themeOff, 0);
        await using (var db = CreateDb(tenantId))
        {
            await db.FormSettings.Where(settings => settings.FormId == formId)
                .ExecuteUpdateAsync(update => update.SetProperty(settings => settings.PdfJson, stored), Cancellation);
        }

        // Act
        var read = await ReadAsync(tenantId, formId);

        // Assert
        read.Pdf.UsesFormTheme.Should().Be(usesFormTheme);
    }

    [Fact]
    public async Task A_second_first_write_is_conceded_and_leaves_nothing_tracked()
    {
        // Arrange: a request found no row and starts one; another request writes the form's first settings first
        var (tenantId, formId) = await InsertFormAsync();
        await using var slowDb = CreateDb(tenantId);
        var slowForm = Form.Create(formId, new FormCreateArgs(TenantId: tenantId, Name: "slow"));
        var slow = FormSettings.For(slowForm);
        slow.Apply(_themeOff, slowForm, new(0, DateTime.UtcNow));
        await UpdateAsync((tenantId, formId), _themeOn, 0);

        // Act
        var saved = await Repository(slowDb).SaveOrConcedeAsync(slow, Cancellation);

        // Assert
        saved.Should().BeFalse();
        slowDb.ChangeTracker.Entries().Should().BeEmpty("the losing insert must not be replayed by a later save");
        (await ReadAsync(tenantId, formId)).Pdf.UsesFormTheme.Should().BeTrue();
    }

    [Fact]
    public async Task Two_first_writes_of_the_same_value_both_succeed()
    {
        // Arrange: a request starts the first row; another request stores the same value first
        var (tenantId, formId) = await InsertFormAsync();
        await using var slowDb = CreateDb(tenantId);
        var slowForms = new EfRepository<Form>(slowDb, new EndatixSpecificationEvaluator([]));
        var slow = new PartialUpdateFormSettingsHandler(slowForms, new RacingFormSettingsRepository(Repository(slowDb), () => UpdateAsync((tenantId, formId), _themeOn, 0)), new DateTimeProvider());

        // Act
        var result = await slow.Handle(new PartialUpdateFormSettingsCommand(formId, 0, _themeOn), Cancellation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(1);
        (await ReadAsync(tenantId, formId)).Version.Should().Be(1);
    }

    [Fact]
    public async Task A_write_that_read_the_same_version_as_a_saved_one_is_conceded()
    {
        // Arrange: two requests load version 1 (theme on); one turns the theme off and saves first
        var (tenantId, formId) = await InsertFormAsync();
        await UpdateAsync((tenantId, formId), _themeOn, 0);
        await using var slowDb = CreateDb(tenantId);
        var slowForm = Form.Create(formId, new FormCreateArgs(TenantId: tenantId, Name: "slow"));
        var slow = (await Repository(slowDb).FindAsync(formId, Cancellation))!;
        await UpdateAsync((tenantId, formId), _themeOff, 1);
        slow.Apply(_themeOff, slowForm, new(1, DateTime.UtcNow)).Value.Should().BeTrue("the slow copy still holds version 1, theme on");

        // Act
        var saved = await Repository(slowDb).SaveOrConcedeAsync(slow, Cancellation);

        // Assert
        saved.Should().BeFalse();
        slowDb.ChangeTracker.Entries().Should().BeEmpty("a refused write must not be replayed by a later save");
        var stored = await ReadAsync(tenantId, formId);
        stored.Version.Should().Be(2);
        stored.Pdf.UsesFormTheme.Should().BeFalse();
    }

    [Fact]
    public async Task Another_tenant_neither_reads_nor_finds_the_settings()
    {
        // Arrange
        var (tenantId, formId) = await InsertFormAsync();
        await UpdateAsync((tenantId, formId), _themeOn, 0);
        var otherTenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        await using var otherDb = CreateDb(otherTenantId);

        // Act
        var found = await Repository(otherDb).FindAsync(formId, Cancellation);
        var read = await Repository(otherDb).ReadAsync(Form.Create(formId, new FormCreateArgs(TenantId: otherTenantId, Name: "other")), Cancellation);

        // Assert
        found.Should().BeNull();
        read.Version.Should().Be(FormSettings.UNSAVED_VERSION);
        read.Pdf.UsesFormTheme.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_the_form_row_deletes_its_settings()
    {
        // Arrange
        var (tenantId, formId) = await InsertFormAsync();
        await UpdateAsync((tenantId, formId), _themeOn, 0);
        await using var db = CreateDb(tenantId);

        // Act: a hard delete of the row, not the soft delete the API does
        await db.Forms.IgnoreQueryFilters().Where(form => form.Id == formId).ExecuteDeleteAsync(Cancellation);

        // Assert
        (await db.FormSettings.IgnoreQueryFilters().AnyAsync(settings => settings.FormId == formId, Cancellation))
            .Should().BeFalse("the settings row lives and dies with its form");
    }

    private async Task<(long TenantId, long FormId)> InsertFormAsync()
    {
        var tenantId = await IntegrationTenants.InsertAsync(fixture.Database, Cancellation);
        await using var db = CreateDb(tenantId);
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: $"settings-{Guid.NewGuid():N}"));
        db.Forms.Add(form);
        await db.SaveChangesAsync(Cancellation);
        return (tenantId, form.Id);
    }

    private async Task<Result<FormSettingsDto>> UpdateAsync((long TenantId, long FormId) form, FormSettingsChange change, long version)
    {
        var (tenantId, formId) = form;
        await using var db = CreateDb(tenantId);
        return await new PartialUpdateFormSettingsHandler(new EfRepository<Form>(db, new EndatixSpecificationEvaluator([])), Repository(db), new DateTimeProvider())
            .Handle(new PartialUpdateFormSettingsCommand(formId, version, change), Cancellation);
    }

    private async Task<FormSettings> ReadAsync(long tenantId, long formId)
    {
        await using var db = CreateDb(tenantId);
        return await Repository(db).ReadAsync(Form.Create(formId, new FormCreateArgs(TenantId: tenantId, Name: "read")), Cancellation);
    }

    private static FormSettingsRepository Repository(AppDbContext db) => new(db);

    private AppDbContext CreateDb(long tenantId)
    {
        DbContextOptionsBuilder<AppDbContext> options = new();
        IntegrationAppDbContextFactory.ConfigureOptions(options, fixture.Database.ConnectionString, fixture.Database.Provider);
        return new AppDbContext(options.Options, new IntegrationTenantContext(tenantId), new OutboxIntegrationEventDispatcher());
    }
}

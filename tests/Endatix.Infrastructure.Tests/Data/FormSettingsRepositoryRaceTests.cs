#pragma warning disable CS0436 // The provider exception stubs in UniqueConstraintViolationCheckerTests stand in for Npgsql's and SqlClient's, which the checker matches by type name.
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Repositories;
using Endatix.Infrastructure.Tests.Features.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;

namespace Endatix.Infrastructure.Tests.Data;

/// <summary>
/// The writer owns the race on a form's settings: <see cref="FormSettingsRepository"/> concedes a stale version or a
/// second first row, for a settings write and for a form save that wrote the settings through the mirror, and stops
/// tracking every pending change of the refused save. <see cref="AppDbContext"/> runs the mirror before the database
/// sees the save, and still reports a second real submission as <see cref="DuplicateSubmissionException"/>.
/// </summary>
public sealed class FormSettingsRepositoryRaceTests
{
    private const string SETTINGS_KEY = "PK_FormSettings";
    private const string SUBMISSION_KEY = "UX_Submissions_RestrictionKey";

    [Fact]
    public async Task UpdateFormOrConcedeAsync_ALostSettingsRace_ConcedesAndStopsTrackingTheSave()
    {
        EntityState? settingsAtRefusal = null;
        await using var context = Refusing(db =>
        {
            settingsAtRefusal = db.ChangeTracker.Entries<FormSettings>().Single().State;
            return new DbUpdateConcurrencyException("stale", PendingSettings(db));
        });
        var form = StoredForm(context);
        var outbox = context.Add(new OutboxMessage("form.updated", "{}", 1, DateTime.UtcNow, 1) { Id = 10 });
        var deleted = context.Attach(NewForm(4));
        deleted.State = EntityState.Deleted;
        var untouched = context.Attach(NewForm(3));
        form.UpdateWebHookSettings(Config());

        var saved = await Repository(context).UpdateFormOrConcedeAsync(form, TestContext.Current.CancellationToken);

        saved.Should().BeFalse();
        settingsAtRefusal.Should().Be(EntityState.Modified, "the mirror copied the form's webhooks before the save");
        context.Entry(form).State.Should().Be(EntityState.Detached);
        outbox.State.Should().Be(EntityState.Detached);
        deleted.State.Should().Be(EntityState.Detached);
        untouched.State.Should().Be(EntityState.Unchanged);
        context.ChangeTracker.HasChanges().Should().BeFalse("a later save must not replay the refused one");
    }

    [Fact]
    public async Task SaveOrConcedeAsync_AStaleSettingsVersion_ConcedesAndStopsTrackingTheSave()
    {
        await using var context = Refusing(db => new DbUpdateConcurrencyException("stale", PendingSettings(db)));
        var form = StoredForm(context);
        var settings = context.FormSettings.Local.Single();
        // Only the PDF section changes, so the tracked form is unchanged.
        settings.Apply(new FormSettingsChange { Pdf = new PdfSettings { UsesFormTheme = true } }, form, new FormSettingsWrite(1, DateTime.UtcNow));

        var saved = await Repository(context).SaveOrConcedeAsync(settings, TestContext.Current.CancellationToken);

        saved.Should().BeFalse();
        context.ChangeTracker.HasChanges().Should().BeFalse();
        context.Entry(form).State.Should().Be(EntityState.Detached, "the settings write reads the winner's form again");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveOrConcedeAsync_ASecondFirstSettingsRow_Concedes(bool onSqlServer)
    {
        Exception providerError = onSqlServer
            ? new Microsoft.Data.SqlClient.SqlException(2627, $"Violation of PRIMARY KEY constraint '{SETTINGS_KEY}'.")
            : new Npgsql.PostgresException("23505", SETTINGS_KEY, null);
        await using var context = Refusing(_ => new DbUpdateException("duplicate", providerError));
        var settings = FormSettings.For(NewForm(2));

        var saved = await Repository(context).SaveOrConcedeAsync(settings, TestContext.Current.CancellationToken);

        saved.Should().BeFalse();
        context.Entry(settings).State.Should().Be(EntityState.Detached);
    }

    [Theory]
    [InlineData("23505", "IX_Forms_Other")]
    [InlineData("42P01", SETTINGS_KEY)]
    [InlineData(null, null)]
    public async Task SaveOrConcedeAsync_AnotherRefusal_IsThrownAndLeavesTheChangesTracked(string? sqlState, string? constraintName)
    {
        await using var context = Refusing(_ => sqlState is null
            ? new DbUpdateException("refused")
            : new DbUpdateException("refused", new Npgsql.PostgresException(sqlState, constraintName!, null)));
        var settings = FormSettings.For(NewForm(2));

        var save = () => Repository(context).SaveOrConcedeAsync(settings, TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<DbUpdateException>();
        context.Entry(settings).State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task UpdateFormOrConcedeAsync_AConcurrencyConflictOnAnotherEntity_IsThrown()
    {
        await using var context = Refusing(db =>
            new DbUpdateConcurrencyException("stale", [.. db.ChangeTracker.Entries<Form>().Select(Infrastructure)]));
        var form = StoredForm(context);
        form.UpdateWebHookSettings(Config());

        var save = () => Repository(context).UpdateFormOrConcedeAsync(form, TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
        context.Entry(form).State.Should().Be(EntityState.Modified);
    }

    [Fact]
    public void SaveChanges_RunsTheMirrorBeforeTheDatabaseSeesTheSave()
    {
        string? sectionAtSave = null;
        using var context = Refusing(db =>
        {
            sectionAtSave = db.ChangeTracker.Entries<FormSettings>().Single().Entity.WebHooksJson;
            return new InvalidOperationException("stop");
        });
        var form = StoredForm(context);
        form.UpdateWebHookSettings(Config());

        var save = () => context.SaveChanges();

        save.Should().Throw<InvalidOperationException>();
        sectionAtSave.Should().Be(form.WebHookSettingsJson);
    }

    [Fact]
    public async Task SaveChangesAsync_ASecondRealSubmission_ThrowsDuplicateSubmission()
    {
        await using var context = Refusing(_ => new DbUpdateException("duplicate", new Npgsql.PostgresException("23505", SUBMISSION_KEY, null)));
        StoredForm(context).UpdateWebHookSettings(Config());

        var save = () => context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<DuplicateSubmissionException>();
    }

    private static FormSettingsRepository Repository(AppDbContext context) =>
        new(context, new EfRepository<Form>(context, new EndatixSpecificationEvaluator([])), new UniqueConstraintViolationChecker());

    // A context whose save fails as the database would refuse it, after the context's own save-time work ran.
    private static AppDbContext Refusing(Func<DbContext, Exception> refusal) =>
        AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext(interceptor: new RefusingInterceptor(refusal));

    // A form with webhooks and its settings, as loaded from the database.
    private static Form StoredForm(AppDbContext context)
    {
        var form = NewForm(2);
        form.UpdateWebHookSettings(Config("https://hooks.example.test/stored"));
        var settings = FormSettings.For(form);
        settings.CopyWebHooksFrom(form, DateTime.UtcNow);
        context.Attach(form);
        context.Attach(settings);
        return form;
    }

    private static WebHookConfiguration Config(string url = "https://hooks.example.test/completed") => new()
    {
        Events = new Dictionary<string, WebHookEventConfig>
        {
            ["SubmissionCompleted"] = new() { IsEnabled = true, WebHookEndpoints = [new WebHookEndpointConfig { Url = url }] },
        },
    };

    // Interceptors passed in the options run before the ones the context adds itself, so this one runs the mirror
    // first, as the context's own registration would, and then refuses the save.
    private sealed class RefusingInterceptor(Func<DbContext, Exception> refusal) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            FormWebHookSettingsMirror.Instance.SavingChanges(eventData, result);
            throw refusal(eventData.Context!);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await FormWebHookSettingsMirror.Instance.SavingChangesAsync(eventData, result, cancellationToken);
            throw refusal(eventData.Context!);
        }
    }

    private static Form NewForm(long id) => Form.Create(id, new FormCreateArgs(TenantId: 1, Name: $"form-{id}"));

    // The entries a refused save reports, as EF Core builds the exception: the pending changes it lists.
#pragma warning disable EF1001 // InternalEntityEntry is the IUpdateEntry EF Core puts on the exception itself.
    private static IUpdateEntry Infrastructure(EntityEntry entry) =>
        entry.GetInfrastructure();

    private static IReadOnlyList<IUpdateEntry> PendingSettings(DbContext context) =>
        [.. context.ChangeTracker.Entries<FormSettings>().Select(Infrastructure)];
#pragma warning restore EF1001
}
#pragma warning restore CS0436

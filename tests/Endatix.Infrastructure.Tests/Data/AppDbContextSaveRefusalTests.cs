#pragma warning disable CS0436 // The provider exception stubs in UniqueConstraintViolationCheckerTests stand in for Npgsql's and SqlClient's, which AppDbContext matches by type name.
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Tests.Features.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;

namespace Endatix.Infrastructure.Tests.Data;

/// <summary>
/// What a refused save means to its caller: <see cref="AppDbContext"/> reports a lost race on a form's settings as
/// <see cref="FormSettingsChangedException"/> and a second real submission as <see cref="DuplicateSubmissionException"/>,
/// and stops tracking the changes of a save that lost the settings race.
/// </summary>
public sealed class AppDbContextSaveRefusalTests : IDisposable
{
    private const string SETTINGS_KEY = "PK_FormSettings";
    private const string SUBMISSION_KEY = "UX_Submissions_RestrictionKey";

    private readonly AppDbContext _context = AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext();

    [Fact]
    public void Translated_AStaleSettingsVersion_IsFormSettingsChanged()
    {
        var conflict = new DbUpdateConcurrencyException("stale", Entries(_context.Attach(Settings(NewForm(2)))));

        AppDbContext.Translated(conflict).Should().BeOfType<FormSettingsChangedException>()
            .Which.InnerException.Should().BeSameAs(conflict);
    }

    [Fact]
    public void Translated_AConcurrencyConflictOnAnotherEntity_IsNotTranslated()
    {
        var conflict = new DbUpdateConcurrencyException("stale", Entries(_context.Attach(NewForm(2))));

        AppDbContext.Translated(conflict).Should().BeNull();
    }

    [Fact]
    public void Translated_ASecondFirstSettingsRowOnPostgreSql_IsFormSettingsChanged()
    {
        var violation = new DbUpdateException("duplicate", new Npgsql.PostgresException("23505", SETTINGS_KEY, null));

        AppDbContext.Translated(violation).Should().BeOfType<FormSettingsChangedException>();
    }

    [Fact]
    public void Translated_ASecondFirstSettingsRowOnSqlServer_IsFormSettingsChanged()
    {
        var violation = new DbUpdateException(
            "duplicate",
            new Microsoft.Data.SqlClient.SqlException(2627, $"Violation of PRIMARY KEY constraint '{SETTINGS_KEY}'."));

        AppDbContext.Translated(violation).Should().BeOfType<FormSettingsChangedException>();
    }

    [Fact]
    public void Translated_ASecondRealSubmission_IsDuplicateSubmission()
    {
        var violation = new DbUpdateException("duplicate", new Npgsql.PostgresException("23505", SUBMISSION_KEY, null));

        AppDbContext.Translated(violation).Should().BeOfType<DuplicateSubmissionException>();
    }

    [Theory]
    [InlineData("23505", "IX_Forms_Other")]
    [InlineData("42P01", SETTINGS_KEY)]
    public void Translated_AnotherViolation_IsNotTranslated(string sqlState, string constraintName)
    {
        var violation = new DbUpdateException("refused", new Npgsql.PostgresException(sqlState, constraintName, null));

        AppDbContext.Translated(violation).Should().BeNull();
    }

    [Fact]
    public void Translated_ASaveWithNoProviderError_IsNotTranslated()
    {
        AppDbContext.Translated(new DbUpdateException("refused")).Should().BeNull();
    }

    [Fact]
    public void Refused_ALostSettingsRace_StopsTrackingThePendingChangesOnly()
    {
        var changed = NewForm(2);
        _context.Attach(changed).State = EntityState.Modified;
        var settings = _context.Add(Settings(changed));
        var untouched = _context.Attach(NewForm(3));

        _context.Refused(new FormSettingsChangedException("lost"));

        _context.Entry(changed).State.Should().Be(EntityState.Detached);
        settings.State.Should().Be(EntityState.Detached);
        untouched.State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public void Refused_ALostSettingsRace_StopsTrackingEveryPendingEntryWhateverItsType()
    {
        // Not only forms and settings: an outbox row the save added and a row it deleted were not stored either.
        var outbox = _context.Add(new OutboxMessage("form.updated", "{}", 1, DateTime.UtcNow, 1) { Id = 10 });
        var deleted = _context.Attach(NewForm(4));
        deleted.State = EntityState.Deleted;

        _context.Refused(new FormSettingsChangedException("lost"));

        outbox.State.Should().Be(EntityState.Detached);
        deleted.State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public void Refused_AnotherRefusal_LeavesTheChangesTracked()
    {
        var changed = NewForm(2);
        _context.Attach(changed).State = EntityState.Modified;

        var refused = _context.Refused(new DuplicateSubmissionException("duplicate"));

        refused.Should().BeOfType<DuplicateSubmissionException>();
        _context.Entry(changed).State.Should().Be(EntityState.Modified);
    }

    [Fact]
    public void SaveChanges_ALostSettingsRace_ThrowsFormSettingsChangedAndStopsTrackingTheSave()
    {
        using var context = Refusing(db => new DbUpdateConcurrencyException("stale", PendingSettings(db)));
        var form = StoredForm(context);
        form.UpdateWebHookSettings(Config());

        var save = () => context.SaveChanges();

        save.Should().Throw<FormSettingsChangedException>();
        context.ChangeTracker.Entries().Should().NotContain(entry => entry.State != EntityState.Unchanged);
    }

    [Fact]
    public async Task SaveChangesAsync_ALostSettingsRace_ThrowsFormSettingsChangedAndStopsTrackingTheSave()
    {
        await using var context = Refusing(db => new DbUpdateConcurrencyException("stale", PendingSettings(db)));
        var form = StoredForm(context);
        form.UpdateWebHookSettings(Config());

        var save = () => context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<FormSettingsChangedException>();
        context.ChangeTracker.Entries().Should().NotContain(entry => entry.State != EntityState.Unchanged);
    }

    [Fact]
    public async Task SaveChangesAsync_ASecondRealSubmission_ThrowsDuplicateSubmission()
    {
        await using var context = Refusing(_ => new DbUpdateException("duplicate", new Npgsql.PostgresException("23505", SUBMISSION_KEY, null)));
        StoredForm(context).UpdateWebHookSettings(Config());

        var save = () => context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<DuplicateSubmissionException>();
    }

    [Fact]
    public void FormSettingsChangedException_KeepsTheProviderError()
    {
        var providerError = new InvalidOperationException("provider");

        var refused = new FormSettingsChangedException("lost", providerError);

        refused.Message.Should().Be("lost");
        refused.InnerException.Should().BeSameAs(providerError);
    }

    public void Dispose() => _context.Dispose();

    // A context whose save fails as the database would refuse it, after the context's own processing ran.
    private static AppDbContext Refusing(Func<DbContext, Exception> refusal) =>
        AppDbContextModelInspectionFactory.CreatePostgreSqlAppDbContext(interceptor: new RefusingInterceptor(refusal));

    // A form with webhooks and its settings, as loaded from the database.
    private static Form StoredForm(AppDbContext context)
    {
        var form = NewForm(2);
        form.UpdateWebHookSettings(Config("https://hooks.example.test/stored"));
        var settings = Settings(form);
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

    private sealed class RefusingInterceptor(Func<DbContext, Exception> refusal) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
            throw refusal(eventData.Context!);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw refusal(eventData.Context!);
    }

    private static Form NewForm(long id) => Form.Create(id, new FormCreateArgs(TenantId: 1, Name: $"form-{id}"));

    private static FormSettings Settings(Form form) => FormSettings.For(form);

    // The entries a refused save reports, as EF Core builds the exception: a pending change (it lists no other).
#pragma warning disable EF1001 // InternalEntityEntry is the IUpdateEntry EF Core puts on the exception itself.
    private static IReadOnlyList<IUpdateEntry> Entries(EntityEntry entry)
    {
        entry.State = EntityState.Modified;
        return [entry.GetInfrastructure()];
    }

    private static IReadOnlyList<IUpdateEntry> PendingSettings(DbContext context) =>
        [.. context.ChangeTracker.Entries<FormSettings>().Select(entry => (IUpdateEntry)entry.GetInfrastructure())];
#pragma warning restore EF1001
}
#pragma warning restore CS0436

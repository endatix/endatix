using System.Data.Common;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.IntegrationTests;

/// <summary>
/// The database guards behind a saved grid view (#1257): two saves made from the same version never both win, even
/// when they interleave past the repository's own version check.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class SubmissionViewRepositoryIntegrationTests(DbIntegrationFixture fixture)
{
    private const long TenantId = 71;
    private const long OtherTenantId = 72;
    private const long FormId = 900;
    private const string Settings = """{"v":1,"columns":[{"key":"q1","width":120}]}""";
    private const string OtherSettings = """{"v":1,"columns":[],"pageSize":50}""";

    [Fact]
    public async Task SaveSharedAsync_CreateRacingAnotherCreate_ConcedesToTheUniqueIndex()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await ResetAsync(ct);
        // The other create commits after this save looked for a view and found none.
        AfterFirstViewRead interleave = new(() => SaveAsync(Save(SubmissionView.UnsavedVersion, OtherSettings), ct));
        await using var racing = CreateContext(TenantId, interleave);

        // Act
        var saved = await Repository(racing).SaveSharedAsync(Save(SubmissionView.UnsavedVersion, Settings), ct);

        // Assert
        interleave.Ran.Should().BeTrue();
        saved.Should().BeNull();
        var stored = await StoredViewsAsync(ct);
        SameJson(stored.Should().ContainSingle().Subject.SettingsJson, OtherSettings).Should().BeTrue();
    }

    [Fact]
    public async Task SaveSharedAsync_UpdateRacingAnotherUpdate_ConcedesToTheVersionToken()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await ResetAsync(ct);
        await SaveAsync(Save(SubmissionView.UnsavedVersion, Settings), ct);
        AfterFirstViewRead interleave = new(() => SaveAsync(Save(1, OtherSettings), ct));
        await using var racing = CreateContext(TenantId, interleave);

        // Act
        var saved = await Repository(racing).SaveSharedAsync(Save(1, Settings), ct);

        // Assert
        interleave.Ran.Should().BeTrue();
        saved.Should().BeNull();
        var stored = (await StoredViewsAsync(ct)).Should().ContainSingle().Subject;
        stored.Version.Should().Be(2);
        SameJson(stored.SettingsJson, OtherSettings).Should().BeTrue();
    }

    [Fact]
    public async Task FindSharedAsync_SameFormIdInAnotherTenant_ReturnsNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await ResetAsync(ct);
        await SaveAsync(Save(SubmissionView.UnsavedVersion, Settings), ct);
        await using var context = CreateContext(OtherTenantId);

        // Act
        var view = await Repository(context).FindSharedAsync(OtherTenantId, FormId, ct);

        // Assert
        view.Should().BeNull();
    }

    private async Task ResetAsync(CancellationToken ct)
    {
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, ct);
        await ReportingTestSchema.EnsureMigratedAsync(fixture.ConnectionString, fixture.Provider, ct);
    }

    private async Task SaveAsync(SubmissionViewSave save, CancellationToken ct)
    {
        await using var context = CreateContext(TenantId);
        (await Repository(context).SaveSharedAsync(save, ct)).Should().NotBeNull();
    }

    private async Task<List<SubmissionView>> StoredViewsAsync(CancellationToken ct)
    {
        await using var context = CreateContext(TenantId);
        return await context.SubmissionViews.AsNoTracking().Where(view => view.FormId == FormId).ToListAsync(ct);
    }

    private ReportingDbContextBase CreateContext(long tenantId, params IInterceptor[] interceptors) =>
        ReportingTestSchema.CreateContext(fixture, new IntegrationTenantContext(tenantId), interceptors);

    private static SubmissionViewRepository Repository(IReportingDbContext context) =>
        new(context, new UniqueConstraintViolationChecker());

    // jsonb stores its own formatting, so compare the JSON, not the text.
    private static bool SameJson(string actual, string expected)
    {
        using var actualDocument = JsonDocument.Parse(actual);
        using var expectedDocument = JsonDocument.Parse(expected);
        return JsonElement.DeepEquals(actualDocument.RootElement, expectedDocument.RootElement);
    }

    private static SubmissionViewSave Save(long version, string settings) =>
        new(TenantId, FormId, version, settings, ModifiedBy: 7);

    /// <summary>Runs another save once, right after the first read of a view, before this context writes.</summary>
    private sealed class AfterFirstViewRead(Func<Task> otherSave) : DbCommandInterceptor
    {
        public bool Ran { get; private set; }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!Ran && command.CommandText.Contains("SubmissionViews", StringComparison.Ordinal) &&
                command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                Ran = true;
                await otherSave();
            }

            return result;
        }
    }
}

using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The batched soft-delete a form's deletion runs on its submissions, on the provider's own SQL: each statement
/// takes at most a batch, so SQL Server never escalates one to a lock on the whole table, and starts after the last
/// id deleted, so none reads again what earlier batches deleted.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed partial class SoftDeleteInBatchesTests(EndatixIntegrationWebHostFixture fixture)
{
    private const int Submissions = 5;
    private const int BatchSize = 2;

    [Fact]
    public async Task SoftDeleteInBatchesAsync_MoreRowsThanABatch_ReadsAndDeletesABatchAStatementAfterTheLastId()
    {
        // Arrange
        var (services, formId) = await FormWithSubmissionsAsync();
        var statements = new UpdateRecorder();
        await using var scope = services.CreateAsyncScope();
        await using var app = AppWith(scope.ServiceProvider, statements);

        // Act
        var deleted = await app.Submissions
            .Where(submission => submission.FormId == formId)
            .SoftDeleteInBatchesAsync(BatchSize, Ct);

        // Assert
        var reads = statements.Reads.ToList();
        var updates = statements.Updates.ToList();
        Assert.Equal(Submissions, deleted);
        Assert.Equal(3, reads.Count);
        Assert.Equal(3, updates.Count);
        Assert.All(reads.Concat(updates), text => Assert.Matches(IdLowerBound(), text));
        Assert.False(await app.Submissions.AnyAsync(submission => submission.FormId == formId, Ct));
    }

    /// <summary>A caller that drops the soft-delete filter still gets batches that end: deleted rows are left out.</summary>
    [Fact]
    public async Task SoftDeleteInBatchesAsync_WithoutTheSoftDeleteFilter_StillEnds()
    {
        // Arrange
        var (services, formId) = await FormWithSubmissionsAsync();
        await using var scope = services.CreateAsyncScope();
        await using var app = AppWith(scope.ServiceProvider, new UpdateRecorder());

        // Act
        var deleted = await app.Submissions
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete])
            .Where(submission => submission.FormId == formId)
            .SoftDeleteInBatchesAsync(BatchSize, Ct)
            .WaitAsync(TimeSpan.FromSeconds(30), Ct);

        // Assert
        Assert.Equal(Submissions, deleted);
    }

    /// <summary>PostgreSQL quotes the column <c>"Id"</c>, SQL Server <c>[Id]</c>.</summary>
    [GeneratedRegex("""["\[]Id["\]] > """)]
    private static partial Regex IdLowerBound();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(IServiceProvider Services, long FormId)> FormWithSubmissionsAsync()
    {
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" },
            Ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: Ct);
        using var form = await admin.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"batches-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q"}]}]}""",
            },
            Ct);
        Assert.Equal(HttpStatusCode.Created, form.StatusCode);
        var formId = long.Parse((await form.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString()!);
        for (var i = 0; i < Submissions; i++)
        {
            using var created = await admin.PostAsJsonAsync(
                $"/api/forms/{formId}/submissions",
                new { isComplete = false, jsonData = "{}", currentPage = 0 },
                Ct);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        return (world.Services, formId);
    }

    /// <summary>The host's <see cref="AppDbContext"/> options plus the recorder.</summary>
    private static AppDbContext AppWith(IServiceProvider services, UpdateRecorder recorder)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>(
                services.GetRequiredService<DbContextOptions<AppDbContext>>())
            .AddInterceptors(recorder)
            .Options;
        return new AppDbContext(
            options,
            services.GetRequiredService<ITenantContext>(),
            services.GetRequiredService<OutboxIntegrationEventDispatcher>());
    }

    /// <summary>Records the statements that read Submissions, and those that update it.</summary>
    private sealed partial class UpdateRecorder : DbCommandInterceptor
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _reads = new();
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _updates = new();

        public IReadOnlyCollection<string> Reads => _reads;

        public IReadOnlyCollection<string> Updates => _updates;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (SubmissionRead().IsMatch(command.CommandText))
            {
                _reads.Enqueue(command.CommandText);
            }

            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (SubmissionUpdate().IsMatch(command.CommandText))
            {
                _updates.Enqueue(command.CommandText);
            }

            return ValueTask.FromResult(result);
        }

        [GeneratedRegex("""FROM ["\[]Submissions["\]]""")]
        private static partial Regex SubmissionRead();

        [GeneratedRegex("""UPDATE ["\[]Submissions["\]]""")]
        private static partial Regex SubmissionUpdate();
    }
}

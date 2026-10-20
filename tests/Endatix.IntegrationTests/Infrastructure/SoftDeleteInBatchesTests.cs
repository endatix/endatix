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
/// takes at most a batch, so SQL Server never escalates one to a lock on the whole table.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed partial class SoftDeleteInBatchesTests(EndatixIntegrationWebHostFixture fixture)
{
    private const int Submissions = 5;
    private const int BatchSize = 2;

    [Fact]
    public async Task SoftDeleteInBatchesAsync_MoreRowsThanABatch_DeletesThemAllABatchAStatement()
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
        Assert.Equal(Submissions, deleted);
        Assert.Equal(3, statements.Count);
        Assert.False(await app.Submissions.AnyAsync(submission => submission.FormId == formId, Ct));
    }

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

    /// <summary>Counts the statements that update Submissions.</summary>
    private sealed partial class UpdateRecorder : DbCommandInterceptor
    {
        private int _count;

        public int Count => _count;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (SubmissionUpdate().IsMatch(command.CommandText))
            {
                Interlocked.Increment(ref _count);
            }

            return ValueTask.FromResult(result);
        }

        [GeneratedRegex("""UPDATE ["\[]Submissions["\]]""")]
        private static partial Regex SubmissionUpdate();
    }
}

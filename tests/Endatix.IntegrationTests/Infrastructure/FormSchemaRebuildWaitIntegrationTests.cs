using Endatix.Infrastructure.Data.Locking;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Persistence.PostgreSql.Locking;
using static Endatix.IntegrationTests.FormSchemaRebuildWorld;

namespace Endatix.IntegrationTests;

/// <summary>
/// Rebuilds of one form's schema that wait for each other against PostgreSQL: what a rebuild does once it has the
/// lock, what it leaves behind when it fails, and what happens when the wait runs out.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class FormSchemaRebuildWaitIntegrationTests(DbIntegrationFixture fixture)
{
    private const int ConcurrentRebuilds = 5;

    private readonly FormSchemaRebuildWorld _world = new(fixture);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concurrent_flattens_on_a_newly_published_version_compile_and_save_the_schema_once()
    {
        // Arrange — only test submissions, so the compile replaces the schema. Every flatten has found the schema
        // older than its submission before the first takes the lock, so the others find the work done once they
        // have it.
        var form = await _world.SeedFormAsync([BaseDefinition, AddsQuestionA]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        RebuildCounter counter = new();
        Rendezvous allFoundItOlder = new(ConcurrentRebuilds);

        // Act
        await Task.WhenAll(Enumerable.Range(0, ConcurrentRebuilds)
            .Select(_ => GetOrCompileNewestCountedAsync(form, counter, allFoundItOlder)));

        // Assert
        counter.Compiles.Should().Be(1);
        counter.Saves.Should().Be(1);
        counter.Deletes.Should().Be(0, "a flatten keeps the flattened rows, its own among them");
        ColumnKeys(await _world.ReadSchemaAsync(form.FormId)).Should().Contain(["q0", "qa"]);
    }

    [Fact]
    public async Task Concurrent_compiles_of_one_new_version_save_the_schema_once()
    {
        // Arrange
        var form = await _world.SeedFormAsync([BaseDefinition, AddsQuestionA]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        RebuildCounter counter = new();

        // Act
        await Task.WhenAll(Enumerable.Range(0, ConcurrentRebuilds)
            .Select(_ => CompileCountedAsync(form.FormId, form.DefinitionIds[1], counter)));

        // Assert
        counter.Saves.Should().Be(1);
        counter.Deletes.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task A_rebuild_that_rolls_back_leaves_the_committed_schema_for_the_next_read_in_its_scope()
    {
        // Arrange — clearing the flattened rows fails after the schema was saved, so the rebuild rolls back.
        var form = await _world.SeedFormAsync([BaseDefinition, AddsQuestionA]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        await using var scope = _world.OpenScope();
        var schemas = scope.Schemas();
        var failingDelete = Substitute.For<IFlattenedSubmissionRepository>();
        failingDelete.DeleteByFormIdAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("Delete failed.")));
        var compile = () => scope.Processor(schemas, failingDelete)
            .ProcessAsync(TenantId, form.FormId, form.DefinitionIds[1], cancellationToken: Cancellation);
        await compile.Should().ThrowAsync<InvalidOperationException>();

        // Act
        var schema = await schemas.GetByFormIdAsync(TenantId, form.FormId, Cancellation);

        // Assert
        schema!.FormDefinitionRevision.Should().Be(form.DefinitionIds[0]);
        ColumnKeys(schema).Should().NotContain("qa");
    }

    [Fact]
    public async Task A_submission_whose_flatten_timed_out_waiting_for_a_rebuild_is_processed_by_the_next_backfill()
    {
        // Arrange — another rebuild of the form holds its lock for the whole first backfill.
        var form = await _world.SeedFormWithRealSubmissionAsync([BaseDefinition]);
        ShortWaitLock shortWait = new(new PostgreSqlTransactionLock());
        SubmissionBackfillResult duringRebuild;
        await using (var rebuild = _world.CreateReportingDbContext())
        {
            await rebuild.Database.BeginTransactionAsync(Cancellation);
            await new PostgreSqlTransactionLock().AcquireAsync(rebuild.Database, RebuildLockOf(form.FormId), Cancellation);
            duringRebuild = await _world.BackfillAsync(form.FormId, shortWait);
        }

        // Act
        var afterRebuild = await _world.BackfillAsync(form.FormId, shortWait);

        // Assert
        duringRebuild.FailedSubmissionIds.Should().Equal(form.SubmissionId);
        afterRebuild.Processed.Should().Be(1);
        (await _world.ReadFlattenedAsync(form.SubmissionId)).DataJson.Should().Contain("first answer");
    }

    [Fact]
    public async Task A_flatten_job_that_times_out_waiting_for_a_rebuild_throws_so_the_job_is_retried()
    {
        // Arrange — another rebuild of the form holds its lock while the job runs.
        var form = await _world.SeedFormWithRealSubmissionAsync([BaseDefinition]);
        await using var rebuild = _world.CreateReportingDbContext();
        await rebuild.Database.BeginTransactionAsync(Cancellation);
        await new PostgreSqlTransactionLock().AcquireAsync(rebuild.Database, RebuildLockOf(form.FormId), Cancellation);
        ShortWaitLock shortWait = new(new PostgreSqlTransactionLock());

        // Act
        var runJob = () => _world.RunFlattenJobAsync(form.FormId, form.SubmissionId, shortWait);

        // Assert — the job runtime retries a job that throws, and never one that returns a failure.
        await runJob.Should().ThrowAsync<TransactionLockTimeoutException>();
    }

    private async Task CompileCountedAsync(long formId, long formDefinitionId, RebuildCounter counter)
    {
        await using var scope = _world.OpenScope();
        CountingSaves schemas = new(scope.Schemas(), counter);
        await scope.Processor(schemas, counter.FlattenedRows)
            .ProcessAsync(TenantId, formId, formDefinitionId, cancellationToken: Cancellation);
    }

    private async Task GetOrCompileNewestCountedAsync(RebuildForm form, RebuildCounter counter, Rendezvous beforeTheLock)
    {
        await using var scope = _world.OpenScope();
        CountingSaves schemas = new(new WaitBeforeLockedRead(scope.Schemas(), beforeTheLock), counter);
        await scope.Provider(schemas, counter)
            .GetOrCompileAsync(TenantId, form.FormId, form.DefinitionIds[^1], Cancellation);
    }
}

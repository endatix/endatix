using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.UseCases.Submissions.ListByFormId;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using Endatix.Modules.Reporting.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class ListSubmissionGridRowsIntegrationTests(DbIntegrationFixture fixture)
{
    private const long TenantId = 71;
    private const long OtherTenantId = 72;

    [Fact]
    public async Task ExecuteAsync_ReturnsStateAndAnswersPerRow()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);

        // Act
        var rows = await ListAsync(Query(TenantId, seed, ["city", "age"]), seed.Page, cancellationToken);

        // Assert
        var prepared = rows.Single(row => row.Id == seed.Prepared);
        prepared.RowState.Should().Be(SubmissionGridRowStates.Prepared);
        prepared.AnswersSource.Should().Be(SubmissionGridAnswerSources.Prepared);
        prepared.Answers["city"].GetString().Should().Be("columbus (prepared)");

        var stale = rows.Single(row => row.Id == seed.Stale);
        stale.RowState.Should().Be(SubmissionGridRowStates.Stale);
        stale.Answers["city"].GetString().Should().Be("Dayton");

        var inProgress = rows.Single(row => row.Id == seed.InProgress);
        inProgress.RowState.Should().Be(SubmissionGridRowStates.None);
        inProgress.AnswersSource.Should().Be(SubmissionGridAnswerSources.Raw);
        inProgress.Answers.Keys.Should().Equal("city");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutColumns_KeepsStatesAndReturnsNoAnswers()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);

        // Act
        var rows = await ListAsync(Query(TenantId, seed, []), seed.Page, cancellationToken);

        // Assert
        rows.Single(row => row.Id == seed.Prepared).RowState.Should().Be(SubmissionGridRowStates.Prepared);
        rows.Should().OnlyContain(row => row.Answers.Count == 0);
    }

    [Fact]
    public async Task ExecuteAsync_ForAnotherTenant_ReadsNoPreparedRows()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);

        // Act
        var rows = await ListAsync(Query(OtherTenantId, seed, ["city"]), seed.Page, cancellationToken);

        // Assert
        rows.Should().OnlyContain(row => row.RowState == SubmissionGridRowStates.None);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTenant_ReturnsUnauthorized()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);
        await using var reportingDb = CreateReportingDbContext(TenantId);
        ListSubmissionGridRows sut = new(new FixedPageMediator(seed.Page), reportingDb);

        // Act
        var result = await sut.ExecuteAsync(Query(0, seed, ["city"]), cancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Unauthorized);
    }

    private async Task<IReadOnlyList<SubmissionGridRow>> ListAsync(
        SubmissionGridRowsQuery query,
        Paged<SubmissionDto> page,
        CancellationToken cancellationToken)
    {
        await using var reportingDb = CreateReportingDbContext(query.TenantId);
        ListSubmissionGridRows sut = new(new FixedPageMediator(page), reportingDb);

        var result = await sut.ExecuteAsync(query, cancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.TotalRecords.Should().Be(page.TotalRecords);
        return result.Value.Items;
    }

    private static SubmissionGridRowsQuery Query(long tenantId, SeededForm seed, IReadOnlyList<string> columns) =>
        new(tenantId, new ListByFormIdQuery(seed.FormId, 1, 10), columns);

    private async Task<SeededForm> SeedAsync(CancellationToken cancellationToken)
    {
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);

        await using var appDb = CreateAppDbContext();
        appDb.Set<Tenant>().Add(new Tenant("grid-rows-tenant", "tnntgrdr") { Id = TenantId });
        await appDb.SaveChangesAsync(cancellationToken);

        var form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "Grid rows form"));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(cancellationToken);
        FormDefinition definition = new(TenantId, isDraft: false, jsonData: """{"pages":[]}""");
        form.AddFormDefinition(definition);
        appDb.Set<FormDefinition>().Add(definition);
        await appDb.SaveChangesAsync(cancellationToken);

        SubmissionSeed submissions = new(appDb, form.Id, definition.Id);
        var prepared = await submissions.AddAsync("""{"city":"Columbus","age":40}""", isComplete: true, cancellationToken);
        var stale = await submissions.AddAsync("""{"city":"Dayton"}""", isComplete: true, cancellationToken);
        var inProgress = await submissions.AddAsync("""{"city":"Akron"}""", isComplete: false, cancellationToken);

        await using var reportingDb = CreateReportingDbContext(TenantId);
        PreparedRowSeed preparedRows = new(reportingDb, appDb, form.Id);
        await preparedRows.PrepareAsync(prepared, """{"city":"columbus (prepared)","age":40}""", cancellationToken);
        await preparedRows.PrepareAsync(stale, """{"city":"old"}""", cancellationToken);
        await appDb.Submissions
            .Where(submission => submission.Id == stale)
            .ExecuteUpdateAsync(updates => updates.SetProperty(s => s.ModifiedAt, DateTime.UtcNow.AddMinutes(5)), cancellationToken);

        var page = await appDb.Submissions
            .AsNoTracking()
            .Where(submission => submission.FormId == form.Id)
            .OrderBy(submission => submission.Id)
            .ToListAsync(cancellationToken);
        var dtos = page.Select(SubmissionDto.FromSubmission).ToList();
        return new SeededForm(form.Id, prepared, stale, inProgress, new Paged<SubmissionDto>(1, 10, dtos.Count, 1, dtos));
    }

    private AppDbContext CreateAppDbContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigureOptions(optionsBuilder, fixture.ConnectionString, fixture.Provider);
        return new AppDbContext(optionsBuilder.Options, new IntegrationTenantContext(TenantId), new OutboxIntegrationEventDispatcher());
    }

    private ReportingDbContextBase CreateReportingDbContext(long tenantId) =>
        ReportingTestSchema.CreateContext(fixture.ConnectionString, fixture.Provider, new IntegrationTenantContext(tenantId));

    private sealed record SeededForm(long FormId, long Prepared, long Stale, long InProgress, Paged<SubmissionDto> Page);

    private sealed class SubmissionSeed(AppDbContext appDb, long formId, long formDefinitionId)
    {
        public async Task<long> AddAsync(string jsonData, bool isComplete, CancellationToken cancellationToken)
        {
            var submission = Submission.Create(new SubmissionCreateArgs(
                TenantId: TenantId,
                FormId: formId,
                FormDefinitionId: formDefinitionId,
                JsonData: jsonData,
                IsComplete: isComplete));
            appDb.Submissions.Add(submission);
            await appDb.SaveChangesAsync(cancellationToken);

            // OwnsOne Status uses shared static instances; clear tracking before the next Add.
            appDb.ChangeTracker.Clear();
            return submission.Id;
        }
    }

    private sealed class PreparedRowSeed(ReportingDbContextBase reportingDb, AppDbContext appDb, long formId)
    {
        public async Task PrepareAsync(long submissionId, string dataJson, CancellationToken cancellationToken)
        {
            await new FlattenedRowSeed(reportingDb).ProcessedAsync(
                new FlattenedSubmissionKey(TenantId, formId, submissionId),
                dataJson,
                cancellationToken);
            var version = await appDb.Submissions
                .Where(submission => submission.Id == submissionId)
                .Select(submission => submission.ModifiedAt ?? submission.CreatedAt)
                .SingleAsync(cancellationToken);
            await reportingDb.FlattenedSubmissions
                .Where(row => row.SubmissionId == submissionId)
                .ExecuteUpdateAsync(updates => updates.SetProperty(row => row.SourceModifiedAt, version), cancellationToken);
        }
    }

    private sealed class FixedPageMediator(Paged<SubmissionDto> page) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            Task.FromResult((TResponse)(object)Result.Success(page));

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}

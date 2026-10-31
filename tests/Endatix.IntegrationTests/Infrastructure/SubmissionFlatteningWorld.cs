using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.IntegrationTests;

/// <summary>
/// A tenant with two forms and live, deleted, completed and draft submissions in the database, and the Reporting
/// pieces that flatten them, shared by the backfill and flatten integration tests.
/// </summary>
internal sealed class SubmissionFlatteningWorld(DbIntegrationFixture fixture)
{
    public const long TenantId = 43;
    public const string DefinitionJson = """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1","title":"Question 1"}]}]}""";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async Task<SeededForms> SeedAsync(CancellationToken cancellationToken)
    {
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);

        await using var appDb = CreateAppDbContext();
        appDb.Set<Tenant>().Add(new Tenant("backfill-scope-tenant", "tnntbfsc") { Id = TenantId });
        await appDb.SaveChangesAsync(cancellationToken);

        var form = await SeedFormAsync(appDb, "Backfill scope form");
        var otherForm = await SeedFormAsync(appDb, "Other form");

        // Interleave kinds so id order alone cannot satisfy a scope.
        var completed1 = await SeedSubmissionAsync(appDb, form, isComplete: true);
        var draft1 = await SeedSubmissionAsync(appDb, form, isComplete: false);
        var deletedCompleted = await SeedSubmissionAsync(appDb, form, isComplete: true);
        var deletedDraft = await SeedSubmissionAsync(appDb, form, isComplete: false);
        await SeedSubmissionAsync(appDb, otherForm, isComplete: false);
        await SeedSubmissionAsync(appDb, otherForm, isComplete: true);
        var completed2 = await SeedSubmissionAsync(appDb, form, isComplete: true);
        var draft2 = await SeedSubmissionAsync(appDb, form, isComplete: false);

        await appDb.Submissions
            .Where(submission => submission.Id == deletedCompleted || submission.Id == deletedDraft)
            .ExecuteUpdateAsync(updates => updates.SetProperty(submission => submission.IsDeleted, true), cancellationToken);

        return new SeededForms(form.FormId, form.DefinitionId, completed1, completed2, draft1, draft2, deletedDraft);
    }

    public AppDbContext CreateAppDbContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigureOptions(optionsBuilder, fixture.ConnectionString, fixture.Provider);
        return new AppDbContext(
            optionsBuilder.Options,
            new IntegrationTenantContext(TenantId),
            new OutboxIntegrationEventDispatcher());
    }

    public ReportingDbContextBase CreateReportingDbContext() =>
        ReportingTestSchema.CreateContext(
            fixture.ConnectionString,
            fixture.Provider,
            new IntegrationTenantContext(TenantId));

    public static SubmissionFlatteningProcessor CreateFlatteningProcessor(AppDbContext appDb, ReportingDbContextBase reportingDb) =>
        CreateFlatteningProcessor(
            CreateSubmissionRepository(appDb),
            reportingDb,
            new FormSchemaProvider(
                CreateSchemaRepository(reportingDb),
                Substitute.For<IFormSchemaProcessor>(),
                new FormSchemaCoverage(Substitute.For<IFormsRepository>(), new FormSchemaCompiler())));

    public static SubmissionFlatteningProcessor CreateFlatteningProcessor(
        IRepository<Submission> submissions,
        ReportingDbContextBase reportingDb,
        IFormSchemaProvider schemaProvider) =>
        new(
            submissions,
            new FlattenedSubmissionRepository(reportingDb, new ReportingUnitOfWork(reportingDb), new DateTimeProvider()),
            schemaProvider,
            new FormSchemaPersonalization(
                Substitute.For<IFormsRepository>(),
                CreateSchemaRepository(reportingDb),
                new ReportingUnitOfWork(reportingDb),
                new FormSchemaCompiler(),
                NullLogger<FormSchemaPersonalization>.Instance),
            SubmitterProfileFields.None,
            ReportingTestSchema.SearchCopyRebuilderFor(reportingDb),
            NullLogger<SubmissionFlatteningProcessor>.Instance);

    public static FormSchemaRepository CreateSchemaRepository(ReportingDbContextBase reportingDb) =>
        new(reportingDb, new ReportingUnitOfWork(reportingDb), ReportingTestSchema.TransactionLockFor(reportingDb));

    public static Task SoftDeleteFormAsync(AppDbContext appDb, long formId, CancellationToken cancellationToken) =>
        appDb.Forms
            .Where(form => form.Id == formId)
            .ExecuteUpdateAsync(updates => updates.SetProperty(form => form.IsDeleted, true), cancellationToken);

    public static FormSchema CompiledSchema(SeededForms seed)
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(DefinitionJson);
        return new FormSchema(TenantId, seed.FormId, seed.DefinitionId, compiled.FlatteningMapJson, compiled.CodebookJson);
    }

    public static EfRepository<Submission> CreateSubmissionRepository(AppDbContext appDb) =>
        new(appDb, new EndatixSpecificationEvaluator([]));

    private static async Task<(long FormId, long DefinitionId)> SeedFormAsync(AppDbContext appDb, string name)
    {
        // Two-step form+definition save avoids the Form ↔ ActiveDefinition circular insert.
        var form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: name));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(Cancellation);

        FormDefinition definition = new(TenantId, isDraft: false, jsonData: DefinitionJson);
        form.AddFormDefinition(definition);
        appDb.Set<FormDefinition>().Add(definition);
        await appDb.SaveChangesAsync(Cancellation);
        return (form.Id, definition.Id);
    }

    private static async Task<long> SeedSubmissionAsync(
        AppDbContext appDb,
        (long FormId, long DefinitionId) form,
        bool isComplete)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: form.FormId,
            FormDefinitionId: form.DefinitionId,
            JsonData: """{"q1":"first"}""",
            IsComplete: isComplete));
        appDb.Submissions.Add(submission);
        await appDb.SaveChangesAsync(Cancellation);

        // OwnsOne Status uses shared static instances; clear tracking before the next Add.
        appDb.ChangeTracker.Clear();
        return submission.Id;
    }

    public sealed record SeededForms(
        long FormId,
        long DefinitionId,
        long Completed1,
        long Completed2,
        long Draft1,
        long Draft2,
        long DeletedDraft);
}

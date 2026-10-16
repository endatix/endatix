using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// <c>submitterProfile.{field}</c> list filters through each provider's own evaluator, as the submissions list runs
/// them. SQL Server answered these with <see cref="NotSupportedException"/> before #1255.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class SubmitterProfileFilterIntegrationTests(DbIntegrationFixture fixture)
{
    private const long TenantId = 61;

    [Fact]
    public async Task List_WithProfileEquals_ReturnsOnlyTheExactMatch()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);

        // Act
        var ids = await ListIdsAsync(seed.FormId, "submitterProfile.email:ann@example.com", cancellationToken);

        // Assert
        ids.Should().BeEquivalentTo([seed.Ann]);
    }

    [Fact]
    public async Task List_WithProfileEquals_IsCaseSensitive()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);

        // Act
        var ids = await ListIdsAsync(seed.FormId, "submitterProfile.email:BOB@example.com|ann@example.com", cancellationToken);

        // Assert
        ids.Should().BeEquivalentTo([seed.Ann]);
    }

    [Fact]
    public async Task List_WithProfileNotEquals_KeepsOtherValuesAndMissingProfiles()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(cancellationToken);

        // Act
        var ids = await ListIdsAsync(seed.FormId, "submitterProfile.email!:ann@example.com", cancellationToken);

        // Assert
        ids.Should().BeEquivalentTo([seed.Bob, seed.NoProfile]);
    }

    private async Task<List<long>> ListIdsAsync(long formId, string filter, CancellationToken cancellationToken)
    {
        using var scope = BuildServices().CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        EndatixSpecificationEvaluator evaluator = new(scope.ServiceProvider.GetServices<IEvaluator>());
        SubmissionsByFormIdSpec spec = new(formId, new PagingParameters(1, 50), new FilterParameters([filter]));

        var rows = await evaluator.GetQuery(appDb.Submissions.AsNoTracking(), spec).ToListAsync(cancellationToken);
        return rows.Select(row => row.Id).ToList();
    }

    private async Task<SeededSubmissions> SeedAsync(CancellationToken cancellationToken)
    {
        await fixture.Checkpoint.ResetAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(fixture.ConnectionString, fixture.Provider, cancellationToken);

        using var scope = BuildServices().CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        appDb.Set<Tenant>().Add(new Tenant("profile-filter-tenant", "tnntprof") { Id = TenantId });
        await appDb.SaveChangesAsync(cancellationToken);

        var form = Form.Create(new FormCreateArgs(TenantId: TenantId, Name: "Profile filter form"));
        appDb.Forms.Add(form);
        await appDb.SaveChangesAsync(cancellationToken);

        FormDefinition definition = new(TenantId, isDraft: false, jsonData: """{"pages":[]}""");
        form.AddFormDefinition(definition);
        appDb.Set<FormDefinition>().Add(definition);
        await appDb.SaveChangesAsync(cancellationToken);

        SubmissionSeed submissions = new(appDb, form.Id, definition.Id);
        return new SeededSubmissions(
            form.Id,
            Ann: await submissions.AddAsync("""{"email":"ann@example.com"}""", cancellationToken),
            Bob: await submissions.AddAsync("""{"email":"bob@example.com"}""", cancellationToken),
            NoProfile: await submissions.AddAsync(profileSnapshot: null, cancellationToken));
    }

    private IServiceProvider BuildServices() =>
        IntegrationCoreMigrationTestHelper.BuildServiceProvider(fixture.ConnectionString, fixture.Provider);

    private sealed record SeededSubmissions(long FormId, long Ann, long Bob, long NoProfile);

    private sealed class SubmissionSeed(AppDbContext appDb, long formId, long formDefinitionId)
    {
        public async Task<long> AddAsync(string? profileSnapshot, CancellationToken cancellationToken)
        {
            var submission = Submission.Create(new SubmissionCreateArgs(
                TenantId: TenantId,
                FormId: formId,
                FormDefinitionId: formDefinitionId,
                JsonData: "{}",
                SubmitterProfileSnapshot: profileSnapshot));
            appDb.Submissions.Add(submission);
            await appDb.SaveChangesAsync(cancellationToken);

            // OwnsOne Status uses shared static instances; clear tracking before the next Add.
            appDb.ChangeTracker.Clear();
            return submission.Id;
        }
    }
}

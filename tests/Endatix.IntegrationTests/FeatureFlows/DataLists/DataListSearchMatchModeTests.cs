using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.DataLists.Search;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Querying;
using Endatix.Infrastructure.Data.Repositories;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Persistence.PostgreSql.Querying;
using Endatix.Persistence.SqlServer.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests.FeatureFlows.DataLists;

/// <summary>
/// Dual-provider FeatureFlow covering Exact / StartsWith / Contains on Labels (locale key), not Value.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class DataListSearchMatchModeTests
{
    private static long _nextId = 10_000;

    private readonly DbIntegrationFixture _fixture;

    public DataListSearchMatchModeTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Exact_MatchesOnlyFullDefaultLabel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            dataListId,
            "Apple",
            skip: 0,
            take: 50,
            DataListSearchMatchMode.Exact,
            locale: null,
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["apple"]);
    }

    [Fact]
    public async Task Exact_DoesNotMatchValueWhenLabelDiffers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        long tenantId = NextId();
        long dataListId;
        await using (AppDbContext seed = CreateAppDbContext(tenantId))
        {
            await EnsureTenantAsync(seed, tenantId, ct);
            DataList list = new(tenantId, $"ValueOnly-{tenantId}") { Id = NextId() };
            list.AddItem("Banana", "special-code");
            seed.DataLists.Add(list);
            await seed.SaveChangesAsync(ct);
            dataListId = list.Id;
        }

        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? byValue = await repository.SearchItemsAsync(
            dataListId,
            "special-code",
            skip: 0,
            take: 50,
            DataListSearchMatchMode.Exact,
            locale: null,
            ct);

        byValue.Should().NotBeNull();
        byValue!.Items.Should().BeEmpty();

        DataListSearchPageResult? byLabel = await repository.SearchItemsAsync(
            dataListId,
            "Banana",
            skip: 0,
            take: 50,
            DataListSearchMatchMode.Exact,
            locale: null,
            ct);

        byLabel.Should().NotBeNull();
        byLabel!.Items.Select(i => i.Value).Should().BeEquivalentTo(["special-code"]);
    }

    [Fact]
    public async Task StartsWith_MatchesPrefixOnDefaultLabel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            dataListId,
            "App",
            skip: 0,
            take: 50,
            DataListSearchMatchMode.StartsWith,
            locale: null,
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["apple", "appetizer"]);
    }

    [Fact]
    public async Task Contains_MatchesSubstringOnDefaultLabel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            dataListId,
            "ppl",
            skip: 0,
            take: 50,
            DataListSearchMatchMode.Contains,
            locale: null,
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["apple", "pineapple"]);
    }

    [Fact]
    public async Task Contains_LocaleEs_MatchesSpanishLabelOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        long tenantId = NextId();
        long dataListId;
        await using (AppDbContext seed = CreateAppDbContext(tenantId))
        {
            await EnsureTenantAsync(seed, tenantId, ct);
            DataList list = new(tenantId, $"Locales-{tenantId}", defaultLocale: "en") { Id = NextId() };
            list.AddCulture("es");
            list.AddItem(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [SurveyJsTranslationKeys.DefaultKey] = "Apple",
                    ["es"] = "Manzana",
                },
                "apple");
            list.AddItem(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [SurveyJsTranslationKeys.DefaultKey] = "Banana",
                    ["es"] = "Plátano",
                },
                "banana");
            seed.DataLists.Add(list);
            await seed.SaveChangesAsync(ct);
            dataListId = list.Id;
        }

        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? esPage = await repository.SearchItemsAsync(
            dataListId,
            "Manz",
            skip: 0,
            take: 50,
            DataListSearchMatchMode.StartsWith,
            locale: "es",
            ct);

        esPage.Should().NotBeNull();
        esPage!.Items.Select(i => i.Value).Should().BeEquivalentTo(["apple"]);

        DataListSearchPageResult? defaultPage = await repository.SearchItemsAsync(
            dataListId,
            "Manz",
            skip: 0,
            take: 50,
            DataListSearchMatchMode.StartsWith,
            locale: null,
            ct);

        defaultPage.Should().NotBeNull();
        defaultPage!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Contains_EscapesLikeMetacharacters()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        long tenantId = NextId();
        long dataListId;
        await using (AppDbContext seed = CreateAppDbContext(tenantId))
        {
            await EnsureTenantAsync(seed, tenantId, ct);
            DataList list = new(tenantId, $"Percents-{tenantId}") { Id = NextId() };
            list.AddItem("100% juice", "pct");
            list.AddItem("100 juice", "plain");
            seed.DataLists.Add(list);
            await seed.SaveChangesAsync(ct);
            dataListId = list.Id;
        }

        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            dataListId,
            "100%",
            skip: 0,
            take: 50,
            DataListSearchMatchMode.Contains,
            locale: null,
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["pct"]);
    }

    [Fact]
    public async Task Contains_EscapesSqlServerCharacterClassBrackets()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        long tenantId = NextId();
        long dataListId;
        await using (AppDbContext seed = CreateAppDbContext(tenantId))
        {
            await EnsureTenantAsync(seed, tenantId, ct);
            DataList list = new(tenantId, $"Brackets-{tenantId}") { Id = NextId() };
            list.AddItem("code[1]", "bracketed");
            list.AddItem("code1", "plain");
            seed.DataLists.Add(list);
            await seed.SaveChangesAsync(ct);
            dataListId = list.Id;
        }

        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            dataListId,
            "code[1]",
            skip: 0,
            take: 50,
            DataListSearchMatchMode.Contains,
            locale: null,
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["bracketed"]);
    }

    [Fact]
    public async Task Contains_EscapesLikeUnderscoreMetacharacter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        long tenantId = NextId();
        long dataListId;
        await using (AppDbContext seed = CreateAppDbContext(tenantId))
        {
            await EnsureTenantAsync(seed, tenantId, ct);
            DataList list = new(tenantId, $"Underscores-{tenantId}") { Id = NextId() };
            list.AddItem("code_1", "literal");
            list.AddItem("codeX1", "wildcard");
            seed.DataLists.Add(list);
            await seed.SaveChangesAsync(ct);
            dataListId = list.Id;
        }

        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            dataListId,
            "code_1",
            skip: 0,
            take: 50,
            DataListSearchMatchMode.Contains,
            locale: null,
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["literal"]);
    }

    private async Task ResetAndMigrateAsync(CancellationToken ct)
    {
        // Migrate first so Respawn can build a table graph on a fresh container.
        IServiceProvider services = IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            _fixture.ConnectionString,
            _fixture.Provider);
        await services.ApplyDbMigrationsAsync(NullLogger.Instance, ct);
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, ct);
    }

    private async Task<(long TenantId, long DataListId)> SeedFruitListAsync(CancellationToken ct)
    {
        long tenantId = NextId();
        await using AppDbContext seed = CreateAppDbContext(tenantId);
        await EnsureTenantAsync(seed, tenantId, ct);
        // "Appetizer" starts with App but does not contain "ppl" (unlike "Appliance").
        DataList list = new(tenantId, $"Fruits-{tenantId}") { Id = NextId() };
        list.AddItem("Apple", "apple");
        list.AddItem("Pineapple", "pineapple");
        list.AddItem("Appetizer", "appetizer");
        list.AddItem("Banana", "banana");
        seed.DataLists.Add(list);
        await seed.SaveChangesAsync(ct);
        return (tenantId, list.Id);
    }

    private static async Task EnsureTenantAsync(AppDbContext db, long tenantId, CancellationToken ct)
    {
        if (await db.Set<Tenant>().AnyAsync(t => t.Id == tenantId, ct))
        {
            return;
        }

        Tenant tenant = new($"datalist-search-tenant-{tenantId}") { Id = tenantId };
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync(ct);
    }

    private static long NextId() => Interlocked.Increment(ref _nextId);

    private AppDbContext CreateAppDbContext(long tenantId)
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        IncrementingIdGenerator idGenerator = new(NextId());
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigureOptions(optionsBuilder, _fixture.ConnectionString, _fixture.Provider);

        return new AppDbContext(
            optionsBuilder.Options,
            idGenerator,
            tenantContext,
            new EfCoreValueGeneratorFactory(idGenerator),
            new OutboxIntegrationEventDispatcher());
    }

    private IDataListRepository CreateRepository(AppDbContext db)
    {
        IRelationalJsonObjectKeyFilter filter = _fixture.Provider == TestDatabaseProvider.SqlServer
            ? new SqlServerJsonObjectKeyFilter()
            : new NpgsqlJsonObjectKeyFilter();
        return new DataListRepository(db, filter);
    }
}

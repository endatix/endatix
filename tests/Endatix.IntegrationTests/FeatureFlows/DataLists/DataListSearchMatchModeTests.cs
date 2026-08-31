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
/// Dual-provider FeatureFlow covering Exact / StartsWith / Contains against the invariant Value
/// and multilingual label keys selected by locale / includeLocales.
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

    private static DataListSearchCriteria Criteria(
        long dataListId,
        string? query,
        DataListSearchMatchMode matchMode,
        string? locale = null,
        IReadOnlyList<string>? includeLocales = null) => new()
        {
            DataListId = dataListId,
            Query = query,
            Skip = 0,
            Take = 50,
            MatchMode = matchMode,
            Locale = string.IsNullOrWhiteSpace(locale) ? null : CultureCode.Parse(locale),
            IncludeLocales = TranslationLocaleList.ParseMany(includeLocales)
        };

    [Fact]
    public async Task SearchItems_ExactDefaultLabel_ReturnsMatchingItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            Criteria(dataListId, "Apple", DataListSearchMatchMode.Exact),
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["apple"]);
    }

    [Fact]
    public async Task SearchItems_ExactOnValueAndLabel_ReturnsMatchAndRejectsPartial()
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
            Criteria(dataListId, "special-code", DataListSearchMatchMode.Exact),
            ct);

        byValue.Should().NotBeNull();
        byValue!.Items.Select(i => i.Value).Should().BeEquivalentTo(["special-code"]);

        DataListSearchPageResult? byLabel = await repository.SearchItemsAsync(
            Criteria(dataListId, "Banana", DataListSearchMatchMode.Exact),
            ct);

        byLabel.Should().NotBeNull();
        byLabel!.Items.Select(i => i.Value).Should().BeEquivalentTo(["special-code"]);

        // Exact still requires a full label match — partial prefix must not hit.
        DataListSearchPageResult? byPartialLabel = await repository.SearchItemsAsync(
            Criteria(dataListId, "Ban", DataListSearchMatchMode.Exact),
            ct);

        byPartialLabel.Should().NotBeNull();
        byPartialLabel!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchItems_StartsWithDefaultLabelPrefix_ReturnsMatchingItems()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            Criteria(dataListId, "App", DataListSearchMatchMode.StartsWith),
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["apple", "appetizer"]);
    }

    [Fact]
    public async Task SearchItems_ContainsDefaultLabelSubstring_ReturnsMatchingItems()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            Criteria(dataListId, "ppl", DataListSearchMatchMode.Contains),
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["apple", "pineapple"]);
    }

    [Fact]
    public async Task SearchItems_StartsWithSpanishLocale_ReturnsSpanishMatchesOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedLocalizedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? esPage = await repository.SearchItemsAsync(
            Criteria(dataListId, "Manz", DataListSearchMatchMode.StartsWith, locale: "es"),
            ct);

        esPage.Should().NotBeNull();
        esPage!.Items.Select(i => i.Value).Should().BeEquivalentTo(["apple"]);

        DataListSearchPageResult? defaultPage = await repository.SearchItemsAsync(
            Criteria(dataListId, "Manz", DataListSearchMatchMode.StartsWith),
            ct);

        defaultPage.Should().NotBeNull();
        defaultPage!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchItems_ContainsSpanishLocaleSubstring_ReturnsMatchAndRejectsStartsWith()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedLocalizedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        // Mid-string in "Manzana" — must not hit under StartsWith, must hit under Contains + locale=es.
        DataListSearchPageResult? startsWith = await repository.SearchItemsAsync(
            Criteria(dataListId, "anzana", DataListSearchMatchMode.StartsWith, locale: "es"),
            ct);

        startsWith.Should().NotBeNull();
        startsWith!.Items.Should().BeEmpty();

        DataListSearchPageResult? contains = await repository.SearchItemsAsync(
            Criteria(dataListId, "anzana", DataListSearchMatchMode.Contains, locale: "es"),
            ct);

        contains.Should().NotBeNull();
        contains!.Items.Select(i => i.Value).Should().BeEquivalentTo(["apple"]);

        DataListSearchPageResult? withoutLocale = await repository.SearchItemsAsync(
            Criteria(dataListId, "anzana", DataListSearchMatchMode.Contains),
            ct);

        withoutLocale.Should().NotBeNull();
        withoutLocale!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchItems_ExactSpanishLocale_ReturnsFullMatchOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedLocalizedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? full = await repository.SearchItemsAsync(
            Criteria(dataListId, "Manzana", DataListSearchMatchMode.Exact, locale: "es"),
            ct);

        full.Should().NotBeNull();
        full!.Items.Select(i => i.Value).Should().BeEquivalentTo(["apple"]);

        DataListSearchPageResult? prefix = await repository.SearchItemsAsync(
            Criteria(dataListId, "Manz", DataListSearchMatchMode.Exact, locale: "es"),
            ct);

        prefix.Should().NotBeNull();
        prefix!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchItems_StartsWithIncludeLocales_ReturnsLocaleMatches()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedLocalizedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            Criteria(dataListId, "Plát", DataListSearchMatchMode.StartsWith, includeLocales: ["es"]),
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["banana"]);
    }

    [Fact]
    public async Task SearchItems_ContainsIncludeLocales_MatchesDefaultAndLocaleLabels()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedLocalizedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        // "ana" is a substring of default "Banana" and of es "Manzana".
        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            Criteria(dataListId, "ana", DataListSearchMatchMode.Contains, includeLocales: ["es"]),
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["apple", "banana"]);

        // Without includeLocales, only default is searched — "Manzana" must not contribute.
        DataListSearchPageResult? defaultOnly = await repository.SearchItemsAsync(
            Criteria(dataListId, "ana", DataListSearchMatchMode.Contains),
            ct);

        defaultOnly.Should().NotBeNull();
        defaultOnly!.Items.Select(i => i.Value).Should().BeEquivalentTo(["banana"]);
    }

    [Fact]
    public async Task SearchItems_ExactWithIncludeLocales_ProjectsRequestedTextKeys()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedLocalizedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? withEs = await repository.SearchItemsAsync(
            Criteria(dataListId, "Apple", DataListSearchMatchMode.Exact, includeLocales: ["es"]),
            ct);

        withEs.Should().NotBeNull();
        withEs!.TextKeys.Should().Equal(SurveyJsTranslationKeys.DefaultKey, "es");

        DataListSearchPageResult? withoutLocales = await repository.SearchItemsAsync(
            Criteria(dataListId, "Apple", DataListSearchMatchMode.Exact),
            ct);

        withoutLocales.Should().NotBeNull();
        withoutLocales!.TextKeys.Should().Equal(SurveyJsTranslationKeys.DefaultKey);
    }

    [Fact]
    public async Task SearchItems_StartsWithWithoutLocale_DoesNotMatchOtherLocales()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await ResetAndMigrateAsync(ct);

        (long tenantId, long dataListId) = await SeedLocalizedFruitListAsync(ct);
        await using AppDbContext db = CreateAppDbContext(tenantId);
        IDataListRepository repository = CreateRepository(db);

        DataListSearchPageResult? page = await repository.SearchItemsAsync(
            Criteria(dataListId, "Plát", DataListSearchMatchMode.StartsWith),
            ct);

        page.Should().NotBeNull();
        page!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchItems_ContainsPercentMetacharacter_EscapesLiteral()
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
            Criteria(dataListId, "100%", DataListSearchMatchMode.Contains),
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["pct"]);
    }

    [Fact]
    public async Task SearchItems_ContainsBracketMetacharacters_EscapesLiteral()
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
            Criteria(dataListId, "code[1]", DataListSearchMatchMode.Contains),
            ct);

        page.Should().NotBeNull();
        page!.Items.Select(i => i.Value).Should().BeEquivalentTo(["bracketed"]);
    }

    [Fact]
    public async Task SearchItems_ContainsUnderscoreMetacharacter_EscapesLiteral()
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
            Criteria(dataListId, "code_1", DataListSearchMatchMode.Contains),
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

    private async Task<(long TenantId, long DataListId)> SeedLocalizedFruitListAsync(CancellationToken ct)
    {
        long tenantId = NextId();
        await using AppDbContext seed = CreateAppDbContext(tenantId);
        await EnsureTenantAsync(seed, tenantId, ct);
        DataList list = new(tenantId, $"Locales-{tenantId}", defaultLocale: "en") { Id = NextId() };
        list.AddCulture(CultureCode.Parse("es"));
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
        return (tenantId, list.Id);
    }

    private static async Task EnsureTenantAsync(AppDbContext db, long tenantId, CancellationToken ct)
    {
        if (await db.Set<Tenant>().AnyAsync(t => t.Id == tenantId, ct))
        {
            return;
        }

        Tenant tenant = new($"datalist-search-tenant-{tenantId}", $"dl{tenantId:D6}") { Id = tenantId };
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

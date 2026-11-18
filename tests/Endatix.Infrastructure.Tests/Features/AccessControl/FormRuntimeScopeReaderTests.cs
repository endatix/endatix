using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.AccessControl;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Features.AccessControl;

/// <summary>
/// The reader reads a form's state (tenant, visibility, enabled, active definition) once per request, so a change
/// takes effect at once on every node. The data lists a definition binds are cached per definition version, so a
/// definition edited in place gets a new entry instead of a stale one. Each reader instance below stands for one
/// request; they share one cache, as the requests of one node do.
/// </summary>
public sealed class FormRuntimeScopeReaderTests : IDisposable
{
    private const long FORM_ID = 7;
    private const long ACTIVE_ID = 100;
    private const long OLDER_ID = 101;
    private const long BOUND_LIST_ID = 11;
    private const long LATER_LIST_ID = 22;
    private const long UNBOUND_LIST_ID = 33;
    private const long OLDER_LIST_ID = 44;

    private static readonly DateTime _savedAt = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);

    private readonly ServiceProvider _services;
    private readonly HybridCache _cache;
    private readonly IRepository<Form> _forms = Substitute.For<IRepository<Form>>();
    private readonly IRepository<FormDefinition> _definitions = Substitute.For<IRepository<FormDefinition>>();
    private readonly CapturingLogger _logger = new();
    private readonly Dictionary<long, (DateTime Version, string Json)> _storedDefinitions = [];
    private FormProjections.FormRuntimeStateDto? _storedForm;

    public FormRuntimeScopeReaderTests()
    {
        _services = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
        _cache = _services.GetRequiredService<HybridCache>();

        _forms
            .SingleOrDefaultAsync(Arg.Any<FormProjections.RuntimeStateSpec>(), Arg.Any<CancellationToken>())
            .Returns(_ => _storedForm);
        _definitions
            .ListAsync(Arg.Any<FormDefinitionProjections.VersionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(_ => _storedDefinitions
                .Select(entry => new FormDefinitionProjections.FormDefinitionVersionDto(entry.Key, entry.Value.Version))
                .ToList());
        _definitions
            .SingleOrDefaultAsync(Arg.Any<FormDefinitionProjections.JsonByIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => _storedDefinitions.TryGetValue(call.Arg<FormDefinitionProjections.JsonByIdSpec>().DefinitionId, out var stored)
                ? stored.Json
                : null);

        StoreDefinition(ACTIVE_ID, _savedAt, BOUND_LIST_ID);
        StoreForm(isPublic: true, isEnabled: true);
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task One_request_reads_the_form_state_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = NewRequest();

        await request.GetAsync(FORM_ID, ct);
        await request.ReachesDataListAsync(FORM_ID, BOUND_LIST_ID, DataListReach.ActiveDefinition, ct);
        await request.GetAsync(FORM_ID, ct);

        await StateReadsShouldBeAsync(1);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task A_visibility_or_enabled_change_is_seen_by_the_next_request(bool isPublic, bool isEnabled)
    {
        var ct = TestContext.Current.CancellationToken;
        (await NewRequest().GetAsync(FORM_ID, ct)).Should().Be(new FormRuntimeScope(FORM_ID, 1, IsPublic: true, IsEnabled: true));

        StoreForm(isPublic, isEnabled);

        (await NewRequest().GetAsync(FORM_ID, ct)).Should().Be(new FormRuntimeScope(FORM_ID, 1, isPublic, isEnabled));
    }

    [Fact]
    public async Task A_deleted_form_is_gone_for_the_next_request_and_reaches_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await NewRequest().ReachesDataListAsync(FORM_ID, BOUND_LIST_ID, DataListReach.ActiveDefinition, ct);

        _storedForm = null;

        var request = NewRequest();
        (await request.GetAsync(FORM_ID, ct)).Should().BeNull();
        (await request.ReachesDataListAsync(FORM_ID, BOUND_LIST_ID, DataListReach.ActiveDefinition, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_definition_version_is_analyzed_once_across_requests()
    {
        var ct = TestContext.Current.CancellationToken;

        (await NewRequest().ReachesDataListAsync(FORM_ID, BOUND_LIST_ID, DataListReach.ActiveDefinition, ct)).Should().BeTrue();
        (await NewRequest().ReachesDataListAsync(FORM_ID, BOUND_LIST_ID, DataListReach.ActiveDefinition, ct)).Should().BeTrue();
        (await NewRequest().ReachesDataListAsync(FORM_ID, UNBOUND_LIST_ID, DataListReach.ActiveDefinition, ct)).Should().BeFalse();

        await JsonReadsShouldBeAsync(1);
    }

    [Fact]
    public async Task A_definition_edited_in_place_is_analyzed_again_without_any_eviction()
    {
        var ct = TestContext.Current.CancellationToken;
        (await NewRequest().ReachesDataListAsync(FORM_ID, LATER_LIST_ID, DataListReach.ActiveDefinition, ct)).Should().BeFalse();

        // Saved on another node: nothing evicts this node's cache, but the version moves.
        StoreDefinition(ACTIVE_ID, _savedAt.AddSeconds(5), LATER_LIST_ID);
        StoreForm(isPublic: true, isEnabled: true);

        var request = NewRequest();
        (await request.ReachesDataListAsync(FORM_ID, LATER_LIST_ID, DataListReach.ActiveDefinition, ct)).Should().BeTrue();
        (await request.ReachesDataListAsync(FORM_ID, BOUND_LIST_ID, DataListReach.ActiveDefinition, ct)).Should().BeFalse("the list is no longer bound");
    }

    [Fact]
    public async Task A_list_bound_only_by_an_older_definition_is_reached_by_any_definition_only()
    {
        var ct = TestContext.Current.CancellationToken;
        StoreDefinition(OLDER_ID, _savedAt.AddDays(-3), OLDER_LIST_ID);
        var request = NewRequest();

        (await request.ReachesDataListAsync(FORM_ID, OLDER_LIST_ID, DataListReach.AnyDefinition, ct)).Should().BeTrue();
        (await request.ReachesDataListAsync(FORM_ID, OLDER_LIST_ID, DataListReach.ActiveDefinition, ct)).Should().BeFalse();
        (await request.ReachesDataListAsync(FORM_ID, BOUND_LIST_ID, DataListReach.AnyDefinition, ct)).Should().BeTrue();
        (await request.ReachesDataListAsync(FORM_ID, UNBOUND_LIST_ID, DataListReach.AnyDefinition, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Any_definition_tries_the_active_definition_before_listing_the_others()
    {
        var ct = TestContext.Current.CancellationToken;
        StoreDefinition(OLDER_ID, _savedAt.AddDays(-3), OLDER_LIST_ID);
        var request = NewRequest();

        (await request.ReachesDataListAsync(FORM_ID, BOUND_LIST_ID, DataListReach.AnyDefinition, ct)).Should().BeTrue();
        await DefinitionListsShouldBeAsync(0);

        (await request.ReachesDataListAsync(FORM_ID, OLDER_LIST_ID, DataListReach.AnyDefinition, ct)).Should().BeTrue();
        await DefinitionListsShouldBeAsync(1);
    }

    [Fact]
    public async Task A_form_without_an_active_definition_reaches_nothing_by_the_active_definition()
    {
        var ct = TestContext.Current.CancellationToken;
        _storedForm = _storedForm! with { ActiveDefinitionId = null, ActiveDefinitionVersion = null };

        (await NewRequest().ReachesDataListAsync(FORM_ID, BOUND_LIST_ID, DataListReach.ActiveDefinition, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_refusal_is_logged_with_the_form_id_only()
    {
        var reaches = await NewRequest().ReachesDataListAsync(FORM_ID, UNBOUND_LIST_ID, DataListReach.ActiveDefinition, TestContext.Current.CancellationToken);

        reaches.Should().BeFalse();
        var (level, message) = _logger.Entries.Should().ContainSingle().Subject;
        level.Should().Be(LogLevel.Information);
        message.Should().Contain(FORM_ID.ToString()).And.NotContain(UNBOUND_LIST_ID.ToString());
    }

    [Fact]
    public async Task A_refusal_for_a_missing_form_is_logged_in_the_same_neutral_words()
    {
        _storedForm = null;

        await NewRequest().ReachesDataListAsync(FORM_ID, BOUND_LIST_ID, DataListReach.ActiveDefinition, TestContext.Current.CancellationToken);

        var (_, message) = _logger.Entries.Should().ContainSingle().Subject;
        message.Should().Be($"Public data-list read refused for form {FORM_ID}: the list is not within the form's reach");
    }

    [Fact]
    public async Task A_reached_list_logs_nothing()
    {
        await NewRequest().ReachesDataListAsync(FORM_ID, BOUND_LIST_ID, DataListReach.ActiveDefinition, TestContext.Current.CancellationToken);

        _logger.Entries.Should().BeEmpty();
    }

    private FormRuntimeScopeReader NewRequest() => new(_forms, _definitions, _cache, _logger);

    private void StoreForm(bool isPublic, bool isEnabled) =>
        _storedForm = new FormProjections.FormRuntimeStateDto(
            FORM_ID,
            TenantId: 1,
            isPublic,
            isEnabled,
            ACTIVE_ID,
            _storedDefinitions[ACTIVE_ID].Version);

    private void StoreDefinition(long definitionId, DateTime version, params long[] listIds) =>
        _storedDefinitions[definitionId] = (version, Definition(listIds));

    private Task StateReadsShouldBeAsync(int count) =>
        _forms.Received(count)
            .SingleOrDefaultAsync(Arg.Any<FormProjections.RuntimeStateSpec>(), Arg.Any<CancellationToken>());

    private Task DefinitionListsShouldBeAsync(int count) =>
        _definitions.Received(count)
            .ListAsync(Arg.Any<FormDefinitionProjections.VersionsByFormIdSpec>(), Arg.Any<CancellationToken>());

    private Task JsonReadsShouldBeAsync(int count) =>
        _definitions.Received(count)
            .SingleOrDefaultAsync(Arg.Any<FormDefinitionProjections.JsonByIdSpec>(), Arg.Any<CancellationToken>());

    private static string Definition(params long[] listIds) =>
        $$"""{"pages":[{"name":"page1","elements":[{{string.Join(",", listIds.Select(id => $$"""{"type":"dropdown","name":"q{{id}}","edxDataListId":"{{id}}"}"""))}}]}]}""";

    private sealed class CapturingLogger : ILogger<FormRuntimeScopeReader>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}

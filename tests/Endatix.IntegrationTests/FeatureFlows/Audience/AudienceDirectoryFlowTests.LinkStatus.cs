using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Each person's Link status on the people list and the update response: none, issued, or opened with the submission
/// while it is live, read in a fixed number of statements per page.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Listing_people_shows_each_persons_link_status()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var mix = await FormWithEveryLinkStatusAsync(world, admin, extraPeople: 0);

        // Act
        var page = await ListPeopleAsync(admin, mix.FormId, string.Empty);

        // Assert
        var links = LinksByIdentifier(page);
        Assert.Equal("none", StatusOf(links["none@example.com"]));
        Assert.Equal("none", StatusOf(links["tester@example.com"]));
        Assert.Equal("issued", StatusOf(links["issued@example.com"]));
        Assert.Equal("issued", StatusOf(links["freed@example.com"]));
        Assert.Equal("opened", StatusOf(links["opened@example.com"]));
        Assert.Equal(mix.OpenedSubmissionId, IdOf(links["opened@example.com"], "submissionId"));
        Assert.True(links["opened@example.com"].TryGetProperty("openedAt", out _));
        Assert.Equal("opened", StatusOf(links["deleted@example.com"]));
        Assert.False(links["deleted@example.com"].TryGetProperty("submissionId", out _));
        Assert.True(links["deleted@example.com"].TryGetProperty("openedAt", out _));
        Assert.False(links["issued@example.com"].TryGetProperty("openedAt", out _));
    }

    [Fact]
    public async Task Updating_a_person_returns_their_link_status()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Akron");
        using var anonymous = world.AnonymousClient();
        var submissionId = IdOf(await RedeemAsync(anonymous, link.FormId, link.Token), "submissionId");

        // Act
        using var updated = await admin.PutValueAsync(link.PersonUrl, link.PropertyId, "Dayton");

        // Assert
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var status = (await admin.ReadAsync(updated)).GetProperty("link");
        Assert.Equal("opened", StatusOf(status));
        Assert.Equal(submissionId, IdOf(status, "submissionId"));
    }

    [Fact]
    public async Task A_page_of_five_thousand_people_costs_the_statements_of_a_page_of_six()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var small = await FormWithEveryLinkStatusAsync(world, admin, extraPeople: 0);
        var large = await FormWithEveryLinkStatusAsync(world, admin, extraPeople: 5_000 - 6);
        var tenantId = world.Tenants[0].Id;

        // Act
        var smallPage = await CountedPageAsync(world.Services, tenantId, small.FormId);
        var largePage = await CountedPageAsync(world.Services, tenantId, large.FormId);

        // Assert
        Assert.Equal(6, smallPage.People);
        Assert.Equal(5_000, largePage.People);
        Assert.Equal(smallPage.Statements, largePage.Statements);
        Assert.Equal(
            [LinkStatusKind.None, LinkStatusKind.Issued, LinkStatusKind.Opened],
            largePage.Kinds.Order().ToArray());
    }

    private sealed record LinkStatusMix(long FormId, long OpenedSubmissionId);

    /// <summary>
    /// A form with one person in each state: no link, a test person, an issued link, an opened one, one whose
    /// submission was marked as test, and one whose submission was deleted; plus <paramref name="extraPeople"/>
    /// issued ones.
    /// </summary>
    private static async Task<LinkStatusMix> FormWithEveryLinkStatusAsync(
        IntegrationTestWorld world,
        AudienceApi admin,
        int extraPeople)
    {
        var formId = await admin.CreateFormAsync();
        await ImportPeopleAsync(admin, formId, extraPeople);
        using var tester = await AddTestPersonAsync(admin, formId, "tester@example.com");
        Assert.Equal(HttpStatusCode.Created, tester.StatusCode);
        var tokens = await IssueLinksByIdentifierAsync(admin, formId);
        using var none = await admin.AddPersonAsync(formId, "none@example.com");
        Assert.Equal(HttpStatusCode.Created, none.StatusCode);

        using var anonymous = world.AnonymousClient();
        var openedId = IdOf(await RedeemAsync(anonymous, formId, tokens["opened@example.com"]), "submissionId");
        var freedId = IdOf(await RedeemAsync(anonymous, formId, tokens["freed@example.com"]), "submissionId");
        var deletedId = IdOf(await RedeemAsync(anonymous, formId, tokens["deleted@example.com"]), "submissionId");
        using var marked = await SetTestAsync(admin, formId, freedId, isTest: true);
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        await DeleteSubmissionAsync(world.Services, deletedId);
        return new LinkStatusMix(formId, openedId);
    }

    private static async Task ImportPeopleAsync(AudienceApi admin, long formId, int extraPeople)
    {
        var csv = new StringBuilder("email\nissued@example.com\nopened@example.com\nfreed@example.com\ndeleted@example.com\n");
        for (var i = 0; i < extraPeople; i++)
        {
            csv.Append('p').Append(i).Append("@example.com\n");
        }

        using var imported = await admin.ImportAsync(formId, csv.ToString());
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
    }

    private static async Task<Dictionary<string, string>> IssueLinksByIdentifierAsync(AudienceApi admin, long formId)
    {
        using var issued = await admin.PostAsync($"/api/forms/{formId}/audience/links", new { });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var tokens = (await admin.ReadAsync(issued)).EnumerateArray()
            .ToDictionary(link => IdOf(link, "membershipId"), link => link.GetProperty("token").GetString()!);
        var page = await ListPeopleAsync(admin, formId, "?pageSize=5000");
        return page.GetProperty("items").EnumerateArray()
            .Where(person => tokens.ContainsKey(IdOf(person, "membershipId")))
            .ToDictionary(
                person => person.GetProperty("identifier").GetString()!,
                person => tokens[IdOf(person, "membershipId")]);
    }

    private static Dictionary<string, JsonElement> LinksByIdentifier(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().ToDictionary(
            person => person.GetProperty("identifier").GetString()!,
            person => person.GetProperty("link"));

    private static string? StatusOf(JsonElement link) => link.GetProperty("status").GetString();

    private sealed record CountedPage(int People, int Statements, LinkStatusKind[] Kinds);

    /// <summary>
    /// The list query run in its own scope, counting the commands that scope's contexts send. Background work on the
    /// host uses other contexts, so it is not counted.
    /// </summary>
    private static async Task<CountedPage> CountedPageAsync(IServiceProvider services, long tenantId, long formId)
    {
        await using var scope = services.CreateAsyncScope();
        DbContext[] contexts =
        [
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            (DbContext)scope.ServiceProvider.GetRequiredService<IAudienceDbContext>(),
        ];
        using var statements = new StatementCounter(contexts);
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new ListPeopleQuery(tenantId, formId, 1, 5_000),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        var page = result.Value;
        return new CountedPage(
            page.Items.Count,
            statements.Count,
            page.Items.Select(person => person.Link.Status).Distinct().ToArray());
    }

    /// <summary>Counts and keeps the commands EF Core runs on the given contexts, from EF's diagnostic events.</summary>
    private sealed class StatementCounter : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly HashSet<DbContext> _contexts;
        private readonly List<IDisposable> _subscriptions = [];
        private readonly ConcurrentQueue<string> _commands = new();

        public StatementCounter(IEnumerable<DbContext> contexts)
        {
            _contexts = contexts.ToHashSet();
            _subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));
        }

        public int Count => _commands.Count;

        /// <summary>The SQL of every command, in the order they ran.</summary>
        public IReadOnlyList<string> Commands => [.. _commands];

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == DbLoggerCategory.Name)
            {
                lock (_subscriptions)
                {
                    _subscriptions.Add(listener.Subscribe(this));
                }
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == RelationalEventId.CommandExecuted.Name
                && value.Value is CommandExecutedEventData { Context: { } context } executed
                && _contexts.Contains(context))
            {
                _commands.Enqueue(executed.Command.CommandText);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            lock (_subscriptions)
            {
                _subscriptions.ForEach(subscription => subscription.Dispose());
            }
        }
    }
}

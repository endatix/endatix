using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Personalized links for the people a search keeps (endatix#1421): the preview counts them, a batch issues links to
/// those without one, and the 5,000 cap counts the links a batch would issue, not the people on the form.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    private const int LINKS_PER_BATCH = 5_000;

    [Fact]
    public async Task Previewing_links_counts_matching_real_participants_and_those_without_a_link()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await FormWithOneLinkedMariaAsync(admin);

        // Act
        var preview = await PreviewLinksAsync(admin, formId, "?search=maria");

        // Assert
        Assert.Equal(2, preview.GetProperty("matching").GetInt32());
        Assert.Equal(1, preview.GetProperty("withoutLink").GetInt32());
        Assert.Equal(LINKS_PER_BATCH, preview.GetProperty("cap").GetInt32());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?search=%20%20")]
    public async Task A_blank_link_preview_counts_every_real_participant(string query)
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await FormWithOneLinkedMariaAsync(admin);

        // Act
        var preview = await PreviewLinksAsync(admin, formId, query);

        // Assert
        Assert.Equal(3, preview.GetProperty("matching").GetInt32());
        Assert.Equal(2, preview.GetProperty("withoutLink").GetInt32());
    }

    [Fact]
    public async Task Previewing_links_with_a_search_longer_than_an_identifier_is_rejected()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();

        // Act
        using var response = await admin.Client.GetAsync(
            $"/api/forms/{formId}/audience/links/preview?search={new string('a', 300)}", admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Issuing_a_link_batch_issues_only_to_matching_real_participants_without_a_link()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await FormWithOneLinkedMariaAsync(admin);
        var mariaTwo = await MembershipIdOfAsync(admin, formId, "maria.two@acme.test");
        using var anonymous = world.AnonymousClient();

        // Act
        var batch = await IssueLinkBatchAsync(admin, formId, "maria");

        // Assert
        Assert.Equal(2, batch.GetProperty("matching").GetInt32());
        var issued = Assert.Single(batch.GetProperty("issued").EnumerateArray());
        Assert.Equal("maria.two@acme.test", issued.GetProperty("identifier").GetString());
        Assert.Equal(mariaTwo, IdOf(issued, "membershipId"));
        var opened = await RedeemAsync(anonymous, formId, issued.GetProperty("token").GetString()!);
        Assert.True(opened.GetProperty("created").GetBoolean());
    }

    [Fact]
    public async Task Issuing_a_link_batch_again_issues_none()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await FormWithOneLinkedMariaAsync(admin);
        await IssueLinkBatchAsync(admin, formId, "maria");

        // Act
        var again = await IssueLinkBatchAsync(admin, formId, "maria");

        // Assert
        Assert.Equal(2, again.GetProperty("matching").GetInt32());
        Assert.Empty(again.GetProperty("issued").EnumerateArray());
    }

    [Fact]
    public async Task Previewing_and_issuing_links_match_the_identifier_ignoring_case()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "Maria@acme.test");
        await AddParticipantAsync(admin, formId, "mario@acme.test");

        // Act
        var preview = await PreviewLinksAsync(admin, formId, "?search=MARIA");
        var batch = await IssueLinkBatchAsync(admin, formId, "mARIA");

        // Assert
        Assert.Equal(1, preview.GetProperty("matching").GetInt32());
        var issued = Assert.Single(batch.GetProperty("issued").EnumerateArray());
        Assert.Equal("Maria@acme.test", issued.GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task Issuing_a_link_batch_on_a_form_over_the_cap_issues_to_the_matching_few()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await SeedRealParticipantsAsync(world.Services, formId, LINKS_PER_BATCH + 1);
        foreach (var identifier in new[] { "pick.1@acme.test", "pick.2@acme.test", "pick.3@acme.test" })
        {
            await AddParticipantAsync(admin, formId, identifier);
        }

        // Act
        var batch = await IssueLinkBatchAsync(admin, formId, "pick.");

        // Assert
        Assert.Equal(3, batch.GetProperty("issued").GetArrayLength());
        using var whole = await admin.PostAsync($"/api/forms/{formId}/audience/links", new { });
        Assert.Equal(HttpStatusCode.BadRequest, whole.StatusCode);
    }

    [Fact]
    public async Task Issuing_a_link_batch_over_the_cap_is_rejected_and_issues_none()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await SeedRealParticipantsAsync(world.Services, formId, LINKS_PER_BATCH + 1);

        // Act
        using var response = await admin.PostAsync(BatchesUrl(formId), new { filter = new { } });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "5,001 people would get a link; the limit is 5,000 per batch. Narrow the filter.",
            await response.Content.ReadAsStringAsync(admin.CancellationToken));
        var preview = await PreviewLinksAsync(admin, formId, "");
        Assert.Equal(LINKS_PER_BATCH + 1, preview.GetProperty("withoutLink").GetInt32());
    }

    [Fact]
    public async Task Issuing_link_batches_at_once_issues_one_link_per_participant()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await SeedRealParticipantsAsync(world.Services, formId, 200);
        var before = await PreviewLinksAsync(admin, formId, "");

        // Act
        var batches = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => IssueLinkBatchAsync(admin, formId, null)));

        // Assert
        var issued = batches.SelectMany(batch => batch.GetProperty("issued").EnumerateArray())
            .Select(item => IdOf(item, "membershipId"))
            .ToList();
        Assert.Equal(before.GetProperty("withoutLink").GetInt32(), issued.Count);
        Assert.Equal(issued.Count, issued.Distinct().Count());
        var after = await PreviewLinksAsync(admin, formId, "");
        Assert.Equal(0, after.GetProperty("withoutLink").GetInt32());
    }

    [Fact]
    public async Task Issuing_link_batches_with_overlapping_filters_at_once_issues_one_link_per_participant()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await SeedRealParticipantsAsync(world.Services, formId, 300);
        var seed = $"seed.{formId}.";
        string[] searches = ["", $"{seed}1", $"{seed}2", seed, $"{seed}1", "", $"{seed}2", $"{seed}3"];

        // Act
        var batches = await Task.WhenAll(searches.Select(search => IssueLinkBatchAsync(admin, formId, search)));

        // Assert
        var issued = batches.SelectMany(batch => batch.GetProperty("issued").EnumerateArray())
            .Select(item => IdOf(item, "membershipId"))
            .ToList();
        Assert.Equal(300, issued.Count);
        Assert.Equal(issued.Count, issued.Distinct().Count());
    }

    [Fact]
    public async Task Issuing_a_link_batch_on_a_form_over_the_cap_whose_people_mostly_have_links_issues_the_rest()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await SeedRealParticipantsAsync(world.Services, formId, LINKS_PER_BATCH + 1);
        var first = await IssueLinkBatchAsync(admin, formId, $"seed.{formId}.1");

        // Act
        var rest = await IssueLinkBatchAsync(admin, formId, null);

        // Assert
        Assert.Equal(1_111, first.GetProperty("issued").GetArrayLength());
        Assert.Equal(LINKS_PER_BATCH + 1 - 1_111, rest.GetProperty("issued").GetArrayLength());
        Assert.Equal(LINKS_PER_BATCH + 1, rest.GetProperty("matching").GetInt32());
    }

    [Fact]
    public async Task Previewing_or_issuing_links_on_another_tenants_form_is_not_found_and_issues_none()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            TestContext.Current.CancellationToken);
        var owner = await AsAsync(world, TestPersona.TenantAdmin);
        var stranger = new AudienceApi(
            await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: owner.CancellationToken),
            owner.CancellationToken);
        var formId = await owner.CreateFormAsync();
        await AddParticipantAsync(owner, formId, "ada@example.com");

        // Act
        using var preview = await stranger.Client.GetAsync(
            $"/api/forms/{formId}/audience/links/preview", owner.CancellationToken);
        using var batch = await stranger.PostAsync(BatchesUrl(formId), new { filter = new { } });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, batch.StatusCode);
        Assert.Equal(1, (await PreviewLinksAsync(owner, formId, "")).GetProperty("withoutLink").GetInt32());
    }

    [Fact]
    public async Task Previewing_or_issuing_links_without_forms_edit_is_forbidden()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword, AfterSeed = SeedViewerAsync },
            TestContext.Current.CancellationToken);
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var viewer = new AudienceApi(
            await world.AsAsync(
                TestPersona.CustomRole(ViewerRole), userName: ViewerUserName, cancellationToken: admin.CancellationToken),
            admin.CancellationToken);
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "ada@example.com");

        // Act
        using var preview = await viewer.Client.GetAsync(
            $"/api/forms/{formId}/audience/links/preview", admin.CancellationToken);
        using var batch = await viewer.PostAsync(BatchesUrl(formId), new { filter = new { } });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, batch.StatusCode);
    }

    /// <summary>
    /// Who gets a link depends on memberships, members and links only. A read of cells would grow with the form's
    /// Properties (ARCHITECTURE, reads that grow with imported data).
    /// </summary>
    [Fact]
    public async Task Previewing_and_issuing_links_read_no_cells()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        (var personUrl, var propertyId) = await admin.PersonWithPropertyAsync(new { name = "City", dataType = "text" });
        using var saved = await admin.PutValueAsync(personUrl, propertyId, "Sofia");
        var formId = long.Parse(personUrl.Split('/')[3]);
        var scope = new LinkBatchScope(await TenantOfFormAsync(world.Services, formId), formId, new PeopleFilter());

        // Act
        var preview = await CountedSendAsync(world.Services, new PreviewLinksQuery(scope));
        var batch = await CountedSendAsync(world.Services, new IssueLinksCommand(scope));

        // Assert
        Assert.Equal(2, preview.Count(command => command.Contains("Memberships", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            preview.Concat(batch),
            command => command.Contains("PropertyValues", StringComparison.Ordinal));
    }

    /// <summary>
    /// A form with two real Marias, the first of whom has a link, a real Ada, and a test Maria.
    /// </summary>
    private static async Task<long> FormWithOneLinkedMariaAsync(AudienceApi admin)
    {
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "maria.one@acme.test");
        await IssueLinkAsync(admin, formId);
        await AddParticipantAsync(admin, formId, "maria.two@acme.test");
        await AddParticipantAsync(admin, formId, "ada@example.com");
        await AddTestParticipantAsync(admin, formId, "maria.test@acme.test");
        return formId;
    }

    private static async Task<long> MembershipIdOfAsync(AudienceApi api, long formId, string identifier)
    {
        var page = await ListPeopleAsync(api, formId, $"?search={Uri.EscapeDataString(identifier)}");
        return IdOf(Assert.Single(page.GetProperty("items").EnumerateArray()), "membershipId");
    }

    /// <summary>
    /// The commands one request sends on its own scope's contexts, as <see cref="StatementCounter"/> sees them.
    /// </summary>
    private static async Task<IReadOnlyList<string>> CountedSendAsync<TResponse>(
        IServiceProvider services,
        IRequest<TResponse> request)
    {
        await using var scope = services.CreateAsyncScope();
        DbContext[] contexts =
        [
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            (DbContext)scope.ServiceProvider.GetRequiredService<IAudienceDbContext>(),
        ];
        using var statements = new StatementCounter(contexts);
        await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(request, TestContext.Current.CancellationToken);
        return statements.Commands;
    }

    private static string BatchesUrl(long formId) => $"/api/forms/{formId}/audience/links/batches";

    private static Task<JsonElement> PreviewLinksAsync(AudienceApi api, long formId, string query) =>
        api.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/audience/links/preview{query}", api.CancellationToken);

    private static async Task<JsonElement> IssueLinkBatchAsync(AudienceApi api, long formId, string? search)
    {
        using var response = await api.PostAsync(BatchesUrl(formId), new { filter = new { search } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await api.ReadAsync(response);
    }

    /// <summary>
    /// Real participants written straight to the tables, so a form can pass the cap in seconds. Their identifiers
    /// start with "seed.", which no search in these tests matches.
    /// </summary>
    private static async Task SeedRealParticipantsAsync(IServiceProvider services, long formId, int count)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var normalizer = scope.ServiceProvider.GetRequiredService<IValueNormalizer>();
        var tenantId = await TenantOfFormAsync(services, formId);
        var members = Enumerable.Range(0, count)
            .Select(index => new Member(new MemberCreateArgs(
                tenantId, $"seed.{formId}.{index}@example.com", AudienceIdentifierKindCodes.Email, normalizer)))
            .ToList();
        audience.Members.AddRange(members);
        await audience.SaveChangesAsync(cancellationToken);
        audience.Memberships.AddRange(members.Select(member =>
            new Membership(new MembershipCreateArgs(tenantId, formId, member.Id, IsTest: false))));
        await audience.SaveChangesAsync(cancellationToken);
    }

    private static async Task<long> TenantOfFormAsync(IServiceProvider services, long formId)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Forms.IgnoreQueryFilters()
            .Where(form => form.Id == formId)
            .Select(form => form.TenantId)
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}

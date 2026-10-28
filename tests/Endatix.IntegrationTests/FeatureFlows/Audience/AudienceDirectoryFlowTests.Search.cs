using System.Net;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Searching a form's people by part of their identifier (endatix#1351), combined with the test filter and paging.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Searching_finds_a_participant_ignoring_case()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "Maria@acme.test");
        await AddParticipantAsync(admin, formId, "mario@acme.test");
        await AddParticipantAsync(admin, formId, "ada@example.com");

        // Act
        var page = await ListPeopleAsync(admin, formId, "?search=MARIA");

        // Assert
        Assert.Equal(["Maria@acme.test"], Identifiers(page));
        Assert.Equal(1, page.GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Searching_never_returns_or_counts_another_tenants_participant()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            TestContext.Current.CancellationToken);
        var owner = await AsAsync(world, TestPersona.TenantAdmin);
        var stranger = new AudienceApi(
            await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: owner.CancellationToken),
            owner.CancellationToken);
        var ownerForm = await owner.CreateFormAsync();
        var strangerForm = await stranger.CreateFormAsync();
        await AddParticipantAsync(owner, ownerForm, "maria@acme.test");
        await AddParticipantAsync(owner, ownerForm, "ada@example.com");
        await AddParticipantAsync(stranger, strangerForm, "maria@acme.test");
        await AddParticipantAsync(stranger, strangerForm, "zoe@elsewhere.test");

        // Act
        var shared = await ListPeopleAsync(owner, ownerForm, "?search=maria");
        var strangersOnly = await ListPeopleAsync(owner, ownerForm, "?search=zoe");

        // Assert
        Assert.Equal(["maria@acme.test"], Identifiers(shared));
        Assert.Equal(1, shared.GetProperty("totalRecords").GetInt32());
        Assert.Empty(Identifiers(strangersOnly));
        Assert.Equal(0, strangersOnly.GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Searching_with_the_test_filter_keeps_only_matching_test_participants_and_pages_them()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "maria.real@example.com");
        await AddTestParticipantAsync(admin, formId, "maria.one@example.com");
        await AddTestParticipantAsync(admin, formId, "maria.two@example.com");
        await AddTestParticipantAsync(admin, formId, "ada@example.com");

        // Act
        var first = await ListPeopleAsync(admin, formId, "?search=maria&test=test&pageSize=1");
        var second = await ListPeopleAsync(admin, formId, "?search=maria&test=test&pageSize=1&page=2");

        // Assert
        Assert.Equal(["maria.one@example.com"], Identifiers(first));
        Assert.Equal(["maria.two@example.com"], Identifiers(second));
        Assert.Equal(2, first.GetProperty("totalRecords").GetInt32());
    }

    [Theory]
    [InlineData("")]
    [InlineData("%20%20")]
    public async Task A_blank_search_lists_everyone(string search)
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "ada@example.com");
        await AddTestParticipantAsync(admin, formId, "bob@example.com");

        // Act
        var page = await ListPeopleAsync(admin, formId, $"?search={search}");

        // Assert
        Assert.Equal(2, page.GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Searching_matches_wildcard_characters_literally()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "a_b@example.com");
        await AddParticipantAsync(admin, formId, "axb@example.com");

        // Act
        var underscore = await ListPeopleAsync(admin, formId, "?search=a_b");
        var percent = await ListPeopleAsync(admin, formId, "?search=a%25b");

        // Assert
        Assert.Equal(["a_b@example.com"], Identifiers(underscore));
        Assert.Empty(Identifiers(percent));
    }

    [Fact]
    public async Task Searching_matches_a_bracket_literally()
    {
        // Arrange
        var admin = await AdminAsync();
        using var key = await admin.PutKeyAsync("external_id");
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "id[1]");
        await AddParticipantAsync(admin, formId, "id1");

        // Act
        var page = await ListPeopleAsync(admin, formId, $"?search={Uri.EscapeDataString("[1]")}");

        // Assert
        Assert.Equal(["id[1]"], Identifiers(page));
    }

    [Theory]
    [InlineData("Élodie")]
    [InlineData("élodie")]
    public async Task Searching_finds_a_non_ascii_identifier(string search)
    {
        // Arrange
        var admin = await AdminAsync();
        using var key = await admin.PutKeyAsync("external_id");
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "Élodie-7");
        await AddParticipantAsync(admin, formId, "Elodie-8");

        // Act
        var page = await ListPeopleAsync(admin, formId, $"?search={Uri.EscapeDataString(search)}");

        // Assert
        Assert.Equal(["Élodie-7"], Identifiers(page));
    }

    [Fact]
    public async Task A_search_of_only_spaces_longer_than_an_identifier_lists_everyone()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        await AddParticipantAsync(admin, formId, "ada@example.com");

        // Act
        var page = await ListPeopleAsync(admin, formId, $"?search={string.Concat(Enumerable.Repeat("%20", 300))}");

        // Assert
        Assert.Equal(1, page.GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Searching_with_a_term_longer_than_an_identifier_is_rejected()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();

        // Act
        using var response = await admin.Client.GetAsync(
            $"/api/forms/{formId}/audience/people?search={new string('a', 300)}", admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

using System.Net;
using System.Text;

namespace Endatix.IntegrationTests;

/// <summary>
/// Searching a form's people by part of their identifier (endatix#1351), combined with the test filter and paging.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Searching_finds_a_participant_among_five_thousand_ignoring_case()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var csv = new StringBuilder("email\nMaria@acme.test\n");
        for (var row = 0; row < 4_999; row++)
        {
            csv.Append("person").Append(row).Append("@example.com\n");
        }

        using var imported = await admin.ImportAsync(formId, csv.ToString());
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);

        // Act
        var page = await ListPeopleAsync(admin, formId, "?search=MARIA");

        // Assert
        Assert.Equal(["Maria@acme.test"], Identifiers(page));
        Assert.Equal(1, page.GetProperty("totalRecords").GetInt32());
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

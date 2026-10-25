using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// The audience directory over HTTP, on whichever provider the run uses: the races the unique
/// indexes and the tenant match-key lock settle, the match-key rules, value checks by data type,
/// and the CSV import (<c>AudienceDirectoryFlowTests.Import.cs</c>).
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed partial class AudienceDirectoryFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private static readonly TimeSpan OutboxWait = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Adding_a_person_keeps_the_email_as_entered()
    {
        // Arrange
        var api = await AdminAsync();
        var formId = await api.CreateFormAsync();

        // Act
        using var created = await api.AddPersonAsync(formId, "Ada@Example.com");

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Ada@Example.com", (await api.ReadAsync(created)).GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task Adding_the_same_email_in_other_casing_to_a_form_is_a_conflict()
    {
        // Arrange
        var api = await AdminAsync();
        var formId = await api.CreateFormAsync();
        using var first = await api.AddPersonAsync(formId, "Ada@Example.com");

        // Act
        using var duplicate = await api.AddPersonAsync(formId, "ada@EXAMPLE.com");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Adding_external_ids_that_differ_only_in_case_adds_two_people()
    {
        // Arrange
        var api = await AdminAsync();
        using var key = await api.PutKeyAsync("external_id");
        var formId = await api.CreateFormAsync();
        using var first = await api.AddPersonAsync(formId, "AbC-1");

        // Act
        using var second = await api.AddPersonAsync(formId, "abc-1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, key.StatusCode);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Adding_a_person_with_an_invalid_email_is_rejected()
    {
        // Arrange
        var api = await AdminAsync();
        var formId = await api.CreateFormAsync();

        // Act
        using var invalid = await api.AddPersonAsync(formId, "John Smith");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Adding_one_person_to_several_forms_in_parallel_reuses_one_member()
    {
        // Arrange
        var api = await AdminAsync();
        var formIds = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => api.CreateFormAsync()));

        // Act
        var responses = await Task.WhenAll(
            formIds.Select(formId => api.AddPersonAsync(formId, "race@example.com")));

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var people = await Task.WhenAll(responses.Select(api.ReadAsync));
        Assert.Single(people.Select(person => IdOf(person, "audienceMemberId")).Distinct());
        DisposeAll(responses);
    }

    [Fact]
    public async Task Adding_one_person_to_a_form_in_parallel_creates_once_and_conflicts_the_rest()
    {
        // Arrange
        var api = await AdminAsync();
        var formId = await api.CreateFormAsync();

        // Act
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => api.AddPersonAsync(formId, "same@example.com")));

        // Assert
        var codes = responses.Select(response => response.StatusCode).ToArray();
        Assert.Single(codes, code => code == HttpStatusCode.Created);
        Assert.All(codes.Where(code => code != HttpStatusCode.Created),
            code => Assert.Equal(HttpStatusCode.Conflict, code));
        DisposeAll(responses);
    }

    [Fact]
    public async Task Saving_the_current_match_key_with_people_on_a_form_succeeds()
    {
        // Arrange
        var api = await AdminAsync();
        await api.AddPersonToNewFormAsync("lock@example.com");

        // Act
        using var response = await api.PutKeyAsync("email");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await api.ReadAsync(response)).GetProperty("isLocked").GetBoolean());
    }

    [Fact]
    public async Task Changing_the_match_key_with_people_on_a_form_is_a_conflict()
    {
        // Arrange
        var api = await AdminAsync();
        await api.AddPersonToNewFormAsync("lock@example.com");

        // Act
        using var response = await api.PutKeyAsync("external_id");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Changing_the_match_key_as_a_creator_is_forbidden()
    {
        // Arrange
        var world = await WorldAsync();
        var creator = await AsAsync(world, TestPersona.Creator);

        // Act
        using var response = await creator.PutKeyAsync("external_id");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Removing_the_last_person_unlocks_the_match_key()
    {
        // Arrange
        var api = await AdminAsync();
        (var formId, var membershipId) = await api.AddPersonToNewFormAsync("lock@example.com");
        using var removed = await api.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/people/{membershipId}", api.CancellationToken);

        // Act
        using var response = await api.PutKeyAsync("external_id");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await api.ReadAsync(response)).GetProperty("isLocked").GetBoolean());
    }

    [Fact]
    public async Task Deleting_a_form_with_people_unlocks_the_match_key()
    {
        // Arrange
        var api = await AdminAsync();
        (var formId, _) = await api.AddPersonToNewFormAsync("gone@example.com");

        // Act
        using var deleted = await api.Client.DeleteAsync($"/api/forms/{formId}", api.CancellationToken);
        var unlocked = await api.WaitUntilUnlockedAsync(OutboxWait);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(unlocked, "The form.deleted outbox handler should remove the form's people.");
    }

    [Fact]
    public async Task Saving_a_value_of_the_wrong_type_is_rejected()
    {
        // Arrange
        var api = await AdminAsync();
        (var personUrl, var propertyId) = await api.PersonWithPropertyAsync(new { name = "Age", dataType = "number" });

        // Act
        using var response = await api.PutValueAsync(personUrl, propertyId, "abc");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Parallel_updates_adding_the_same_cell_all_succeed()
    {
        // Arrange
        var api = await AdminAsync();
        (var personUrl, var propertyId) = await api.PersonWithPropertyAsync(new { name = "Age", dataType = "number" });

        // Act
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(i => api.PutValueAsync(personUrl, propertyId, $"{40 + i}")));

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        DisposeAll(responses);
    }

    [Fact]
    public async Task Saving_an_empty_value_clears_the_cell()
    {
        // Arrange
        var api = await AdminAsync();
        (var personUrl, var propertyId) = await api.PersonWithPropertyAsync(new { name = "City", dataType = "text" });
        using var saved = await api.PutValueAsync(personUrl, propertyId, "Sofia");

        // Act
        using var cleared = await api.PutValueAsync(personUrl, propertyId, "");

        // Assert
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Empty((await api.ReadAsync(cleared)).GetProperty("values").EnumerateObject());
    }

    [Theory]
    [InlineData("single_choice", null)]
    [InlineData("text", """["a"]""")]
    public async Task Creating_a_property_with_choice_settings_that_do_not_fit_its_type_is_rejected(
        string dataType,
        string? choicesJson)
    {
        // Arrange
        var api = await AdminAsync();
        var formId = await api.CreateFormAsync();

        // Act
        using var response = await api.PostAsync(
            $"/api/forms/{formId}/audience/properties", new { name = "Plan", dataType, choicesJson });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_choice_property_with_keys_succeeds()
    {
        // Arrange
        var api = await AdminAsync();
        var formId = await api.CreateFormAsync();

        // Act
        using var response = await api.PostAsync(
            $"/api/forms/{formId}/audience/properties",
            new { name = "Tier", dataType = "single_choice", choicesJson = """["basic","pro"]""" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_property_removes_its_values_from_the_people_list()
    {
        // Arrange
        var api = await AdminAsync();
        (var personUrl, var propertyId) = await api.PersonWithPropertyAsync(new { name = "City", dataType = "text" });
        using var saved = await api.PutValueAsync(personUrl, propertyId, "Sofia");
        var formUrl = personUrl[..personUrl.IndexOf("/audience/", StringComparison.Ordinal)];

        // Act
        using var deleted = await api.Client.DeleteAsync(
            $"{formUrl}/audience/properties/{propertyId}", api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var page = await api.Client.GetFromJsonAsync<JsonElement>($"{formUrl}/audience/people", api.CancellationToken);
        var person = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Empty(person.GetProperty("values").EnumerateObject());
    }

    private Task<IntegrationTestWorld> WorldAsync() => WorldAsync(fixture);

    private static Task<IntegrationTestWorld> WorldAsync(IIntegrationTestHostFixture host) =>
        host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            TestContext.Current.CancellationToken);

    private async Task<AudienceApi> AdminAsync() => await AsAsync(await WorldAsync(), TestPersona.TenantAdmin);

    private static async Task<AudienceApi> AsAsync(IntegrationTestWorld world, TestPersona persona)
    {
        var ct = TestContext.Current.CancellationToken;
        return new AudienceApi(await world.AsAsync(persona, cancellationToken: ct), ct);
    }

    /// <summary>Ids may be sent as JSON strings (snowflake) or numbers.</summary>
    private static long IdOf(JsonElement element, string name)
    {
        var id = element.GetProperty(name);
        return id.ValueKind == JsonValueKind.String ? long.Parse(id.GetString()!) : id.GetInt64();
    }

    private static void DisposeAll(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    /// <summary>
    /// Audience API calls for one signed-in client.
    /// </summary>
    /// <summary>A CSV to import, its Property columns by header, and whether it adds Test participants.</summary>
    private sealed record ImportedCsv(string Text, Dictionary<string, string>? PropertyColumns = null, bool IsTest = false);

    private sealed partial class AudienceApi(HttpClient client, CancellationToken cancellationToken)
    {
        private const string FormDefinition =
            """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}""";

        public HttpClient Client => client;

        public CancellationToken CancellationToken => cancellationToken;

        /// <summary>
        /// A form with no token expiry of its own: the test tenant has no tenant settings either, so
        /// every submission token here takes the new-tenant default.
        /// </summary>
        public async Task<long> CreateFormAsync(bool limitOnePerUser = false, string? definition = null)
        {
            using var response = await PostAsync(
                "/api/forms",
                new
                {
                    name = $"Audience {Guid.NewGuid():N}",
                    isEnabled = true,
                    limitOnePerUser,
                    formDefinitionJsonData = definition ?? FormDefinition,
                });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return IdOf(await ReadAsync(response), "id");
        }

        public async Task<long> CreatePropertyAsync(long formId, object body)
        {
            using var response = await PostAsync($"/api/forms/{formId}/audience/properties", body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return IdOf(await ReadAsync(response), "id");
        }

        public async Task<(long FormId, long MembershipId)> AddPersonToNewFormAsync(string identifier)
        {
            var formId = await CreateFormAsync();
            using var response = await AddPersonAsync(formId, identifier);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (formId, IdOf(await ReadAsync(response), "membershipId"));
        }

        /// <summary>A person on a new form that has one property; returns the person's URL.</summary>
        public async Task<(string PersonUrl, long PropertyId)> PersonWithPropertyAsync(
            object property,
            string? identifier = null,
            bool limitOnePerUser = false)
        {
            var formId = await CreateFormAsync(limitOnePerUser);
            var propertyId = await CreatePropertyAsync(formId, property);
            using var response = await AddPersonAsync(formId, identifier ?? $"{Guid.NewGuid():N}@example.com");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var membershipId = IdOf(await ReadAsync(response), "membershipId");
            return ($"/api/forms/{formId}/audience/people/{membershipId}", propertyId);
        }

        public Task<HttpResponseMessage> ImportAsync(
            long formId,
            string csv,
            Dictionary<string, string>? propertyColumns = null) =>
            ImportAsync(formId, new ImportedCsv(csv, propertyColumns));

        public Task<HttpResponseMessage> ImportAsync(long formId, ImportedCsv csv) =>
            PostAsync(
                $"/api/forms/{formId}/audience/import",
                new
                {
                    csvText = csv.Text,
                    identifierColumn = "email",
                    fileName = "people.csv",
                    propertyColumns = csv.PropertyColumns,
                    isTest = csv.IsTest,
                });

        public Task<HttpResponseMessage> AddPersonAsync(long formId, string identifier) =>
            PostAsync($"/api/forms/{formId}/audience/people", new { identifier });

        public Task<HttpResponseMessage> PutKeyAsync(string kind) =>
            client.PutAsJsonAsync("/api/audience/settings", new { identifierKind = kind }, cancellationToken);

        public Task<HttpResponseMessage> PutValueAsync(string personUrl, long propertyId, string value) =>
            client.PutAsJsonAsync(
                personUrl,
                new { values = new Dictionary<string, string> { [$"{propertyId}"] = value } },
                cancellationToken);

        public Task<HttpResponseMessage> PostAsync(string url, object body) =>
            client.PostAsJsonAsync(url, body, cancellationToken);

        public Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
            response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        /// <summary>Polls the settings until the match key unlocks; outbox handlers run in the background.</summary>
        public async Task<bool> WaitUntilUnlockedAsync(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var settings = await client.GetFromJsonAsync<JsonElement>("/api/audience/settings", cancellationToken);
                if (!settings.GetProperty("isLocked").GetBoolean())
                {
                    return true;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }

            return false;
        }
    }
}

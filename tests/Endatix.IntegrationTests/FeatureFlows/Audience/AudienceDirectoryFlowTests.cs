using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// The audience directory over HTTP on PostgreSQL: the races the unique indexes and the tenant
/// match-key lock settle, the match-key rules, value checks by data type, and the CSV import. The module is
/// PostgreSQL-only, so every test skips on SQL Server.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class AudienceDirectoryFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private static readonly TimeSpan OutboxWait = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Adding_a_person_keeps_the_email_as_entered()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();

        // Act
        using HttpResponseMessage created = await api.AddPersonAsync(formId, "Ada@Example.com");

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Ada@Example.com", (await api.ReadAsync(created)).GetProperty("identifier").GetString());
    }

    [Fact]
    public async Task Adding_the_same_email_in_other_casing_to_a_form_is_a_conflict()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        using HttpResponseMessage first = await api.AddPersonAsync(formId, "Ada@Example.com");

        // Act
        using HttpResponseMessage duplicate = await api.AddPersonAsync(formId, "ada@EXAMPLE.com");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Adding_a_person_with_an_invalid_email_is_rejected()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();

        // Act
        using HttpResponseMessage invalid = await api.AddPersonAsync(formId, "John Smith");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Adding_one_person_to_several_forms_in_parallel_reuses_one_member()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long[] formIds = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => api.CreateFormAsync()));

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(
            formIds.Select(formId => api.AddPersonAsync(formId, "race@example.com")));

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        JsonElement[] people = await Task.WhenAll(responses.Select(api.ReadAsync));
        Assert.Single(people.Select(person => IdOf(person, "audienceMemberId")).Distinct());
        DisposeAll(responses);
    }

    [Fact]
    public async Task Adding_one_person_to_a_form_in_parallel_creates_once_and_conflicts_the_rest()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => api.AddPersonAsync(formId, "same@example.com")));

        // Assert
        HttpStatusCode[] codes = responses.Select(response => response.StatusCode).ToArray();
        Assert.Single(codes, code => code == HttpStatusCode.Created);
        Assert.All(codes.Where(code => code != HttpStatusCode.Created),
            code => Assert.Equal(HttpStatusCode.Conflict, code));
        DisposeAll(responses);
    }

    [Fact]
    public async Task Saving_the_current_match_key_with_people_on_a_form_succeeds()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        await api.AddPersonToNewFormAsync("lock@example.com");

        // Act
        using HttpResponseMessage response = await api.PutKeyAsync("email");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await api.ReadAsync(response)).GetProperty("isLocked").GetBoolean());
    }

    [Fact]
    public async Task Changing_the_match_key_with_people_on_a_form_is_a_conflict()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        await api.AddPersonToNewFormAsync("lock@example.com");

        // Act
        using HttpResponseMessage response = await api.PutKeyAsync("external_id");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Changing_the_match_key_as_a_creator_is_forbidden()
    {
        // Arrange
        IntegrationTestWorld world = await WorldAsync();
        AudienceApi creator = await AsAsync(world, TestPersona.Creator);

        // Act
        using HttpResponseMessage response = await creator.PutKeyAsync("external_id");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Removing_the_last_person_unlocks_the_match_key()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        (long formId, long membershipId) = await api.AddPersonToNewFormAsync("lock@example.com");
        using HttpResponseMessage removed = await api.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/people/{membershipId}", api.CancellationToken);

        // Act
        using HttpResponseMessage response = await api.PutKeyAsync("external_id");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await api.ReadAsync(response)).GetProperty("isLocked").GetBoolean());
    }

    [Fact]
    public async Task Deleting_a_form_with_people_unlocks_the_match_key()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        (long formId, _) = await api.AddPersonToNewFormAsync("gone@example.com");

        // Act
        using HttpResponseMessage deleted = await api.Client.DeleteAsync($"/api/forms/{formId}", api.CancellationToken);
        bool unlocked = await api.WaitUntilUnlockedAsync(OutboxWait);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(unlocked, "The form.deleted outbox handler should remove the form's people.");
    }

    [Fact]
    public async Task Saving_a_value_of_the_wrong_type_is_rejected()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        (string personUrl, long propertyId) = await api.PersonWithPropertyAsync(new { name = "Age", dataType = "number" });

        // Act
        using HttpResponseMessage response = await api.PutValueAsync(personUrl, propertyId, "abc");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Parallel_updates_adding_the_same_cell_all_succeed()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        (string personUrl, long propertyId) = await api.PersonWithPropertyAsync(new { name = "Age", dataType = "number" });

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(i => api.PutValueAsync(personUrl, propertyId, $"{40 + i}")));

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        DisposeAll(responses);
    }

    [Fact]
    public async Task Saving_an_empty_value_clears_the_cell()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        (string personUrl, long propertyId) = await api.PersonWithPropertyAsync(new { name = "City", dataType = "text" });
        using HttpResponseMessage saved = await api.PutValueAsync(personUrl, propertyId, "Sofia");

        // Act
        using HttpResponseMessage cleared = await api.PutValueAsync(personUrl, propertyId, "");

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
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();

        // Act
        using HttpResponseMessage response = await api.PostAsync(
            $"/api/forms/{formId}/audience/properties", new { name = "Plan", dataType, choicesJson });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_choice_property_with_keys_succeeds()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();

        // Act
        using HttpResponseMessage response = await api.PostAsync(
            $"/api/forms/{formId}/audience/properties",
            new { name = "Tier", dataType = "single_choice", choicesJson = """["basic","pro"]""" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_property_removes_its_values_from_the_people_list()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        (string personUrl, long propertyId) = await api.PersonWithPropertyAsync(new { name = "City", dataType = "text" });
        using HttpResponseMessage saved = await api.PutValueAsync(personUrl, propertyId, "Sofia");
        string formUrl = personUrl[..personUrl.IndexOf("/audience/", StringComparison.Ordinal)];

        // Act
        using HttpResponseMessage deleted = await api.Client.DeleteAsync(
            $"{formUrl}/audience/properties/{propertyId}", api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        JsonElement page = await api.Client.GetFromJsonAsync<JsonElement>($"{formUrl}/audience/people", api.CancellationToken);
        JsonElement person = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Empty(person.GetProperty("values").EnumerateObject());
    }

    [Fact]
    public async Task Importing_an_existing_email_in_other_casing_matches_that_person()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        (long formId, _) = await api.AddPersonToNewFormAsync("Ada@Example.com");

        // Act
        using HttpResponseMessage response = await api.ImportAsync(formId, "email\nada@example.com\ncy@example.com\n");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement result = await api.ReadAsync(response);
        // The row has no values, so the matched person is counted as skipped, not created.
        Assert.Equal(1, result.GetProperty("skippedCount").GetInt32());
        Assert.Equal(1, result.GetProperty("createdCount").GetInt32());
    }

    [Fact]
    public async Task Importing_bad_rows_rejects_them_with_their_row_numbers()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        await api.CreatePropertyAsync(formId, new { name = "Age", dataType = "number" });
        const string csv = "email,Age\nada@example.com,36\nJohn Smith,40\nbob@example.com,forty\n";

        // Act
        using HttpResponseMessage response = await api.ImportAsync(
            formId, csv, new Dictionary<string, string> { ["age"] = "Age" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        int[] rejectedRows = (await api.ReadAsync(response)).GetProperty("rejections").EnumerateArray()
            .Select(rejection => rejection.GetProperty("rowNumber").GetInt32())
            .ToArray();
        Assert.Equal([3, 4], rejectedRows);
    }

    [Fact]
    public async Task Importing_without_the_identifier_column_writes_nothing()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();

        // Act
        using HttpResponseMessage response = await api.ImportAsync(formId, "name\nAda\n");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonElement page = await api.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/audience/people", api.CancellationToken);
        Assert.Equal(0, page.GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Changing_the_match_key_after_an_import_is_a_conflict()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        using HttpResponseMessage imported = await api.ImportAsync(formId, "email\nada@example.com\n");
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);

        // Act
        using HttpResponseMessage response = await api.PutKeyAsync("external_id");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Parallel_imports_of_the_same_people_on_several_forms_all_succeed()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long[] formIds = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => api.CreateFormAsync()));
        string csv = "email\n" + string.Join("\n", Enumerable.Range(0, 50).Select(i => $"p{i}@example.com"));

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(formIds.Select(formId => api.ImportAsync(formId, csv)));

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        JsonElement[] results = await Task.WhenAll(responses.Select(api.ReadAsync));
        Assert.All(results, result => Assert.Equal(50, result.GetProperty("createdCount").GetInt32()));
        DisposeAll(responses);
    }

    private async Task<IntegrationTestWorld> WorldAsync()
    {
        Assert.SkipWhen(
            fixture.Provider != TestDatabaseProvider.PostgreSql,
            "The audience module is PostgreSQL-only; it is not registered on this provider.");
        return await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            TestContext.Current.CancellationToken);
    }

    private async Task<AudienceApi> AdminAsync() => await AsAsync(await WorldAsync(), TestPersona.TenantAdmin);

    private static async Task<AudienceApi> AsAsync(IntegrationTestWorld world, TestPersona persona)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        return new AudienceApi(await world.AsAsync(persona, cancellationToken: ct), ct);
    }

    /// <summary>Ids may be sent as JSON strings (snowflake) or numbers.</summary>
    private static long IdOf(JsonElement element, string name)
    {
        JsonElement id = element.GetProperty(name);
        return id.ValueKind == JsonValueKind.String ? long.Parse(id.GetString()!) : id.GetInt64();
    }

    private static void DisposeAll(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }
    }

    /// <summary>
    /// Audience API calls for one signed-in client.
    /// </summary>
    private sealed class AudienceApi(HttpClient client, CancellationToken cancellationToken)
    {
        private const string FormDefinition =
            """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}""";

        public HttpClient Client => client;

        public CancellationToken CancellationToken => cancellationToken;

        public async Task<long> CreateFormAsync()
        {
            using HttpResponseMessage response = await PostAsync(
                "/api/forms",
                new { name = $"Audience {Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = FormDefinition });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return IdOf(await ReadAsync(response), "id");
        }

        public async Task<long> CreatePropertyAsync(long formId, object body)
        {
            using HttpResponseMessage response = await PostAsync($"/api/forms/{formId}/audience/properties", body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return IdOf(await ReadAsync(response), "id");
        }

        public async Task<(long FormId, long MembershipId)> AddPersonToNewFormAsync(string identifier)
        {
            long formId = await CreateFormAsync();
            using HttpResponseMessage response = await AddPersonAsync(formId, identifier);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (formId, IdOf(await ReadAsync(response), "membershipId"));
        }

        /// <summary>A person on a new form that has one property; returns the person's URL.</summary>
        public async Task<(string PersonUrl, long PropertyId)> PersonWithPropertyAsync(object property)
        {
            long formId = await CreateFormAsync();
            long propertyId = await CreatePropertyAsync(formId, property);
            using HttpResponseMessage response = await AddPersonAsync(formId, $"{Guid.NewGuid():N}@example.com");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            long membershipId = IdOf(await ReadAsync(response), "membershipId");
            return ($"/api/forms/{formId}/audience/people/{membershipId}", propertyId);
        }

        public Task<HttpResponseMessage> ImportAsync(
            long formId,
            string csv,
            Dictionary<string, string>? propertyColumns = null) =>
            PostAsync(
                $"/api/forms/{formId}/audience/import",
                new { csvText = csv, identifierColumn = "email", fileName = "people.csv", propertyColumns });

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
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                JsonElement settings = await client.GetFromJsonAsync<JsonElement>("/api/audience/settings", cancellationToken);
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

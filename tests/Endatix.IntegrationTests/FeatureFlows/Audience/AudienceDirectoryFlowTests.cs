using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// The audience directory over HTTP on PostgreSQL: the races the unique indexes and the tenant
/// match-key lock settle, the match-key rules, and value checks by data type.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class AudienceDirectoryFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";

    [Fact]
    public async Task CreatePerson_EmailInMixedCase_KeepsCasingAndMatchesIgnoringCase()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();

        // Act
        using HttpResponseMessage created = await api.AddPersonAsync(formId, "Ada@Example.com");
        using HttpResponseMessage duplicate = await api.AddPersonAsync(formId, "ada@EXAMPLE.com");
        using HttpResponseMessage invalid = await api.AddPersonAsync(formId, "John Smith");

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonElement person = await api.ReadAsync(created);
        Assert.Equal("Ada@Example.com", person.GetProperty("identifier").GetString());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task CreatePerson_ParallelOnSeveralForms_ReusesOneMember()
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
    public async Task CreatePerson_ParallelOnOneForm_CreatesOnceAndConflictsTheRest()
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
    public async Task UpdateSettings_MatchKey_FollowsLockRules()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        AudienceApi admin = new(await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct), ct);
        AudienceApi creator = new(await world.AsAsync(TestPersona.Creator, cancellationToken: ct), ct);
        long formId = await admin.CreateFormAsync();
        using HttpResponseMessage added = await admin.AddPersonAsync(formId, "lock@example.com");
        long membershipId = IdOf(await admin.ReadAsync(added), "membershipId");

        // Act
        using HttpResponseMessage sameKey = await admin.PutKeyAsync("email");
        using HttpResponseMessage lockedChange = await admin.PutKeyAsync("external_id");
        using HttpResponseMessage byCreator = await creator.PutKeyAsync("external_id");
        using HttpResponseMessage removed = await admin.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/people/{membershipId}", ct);
        JsonElement afterRemove = await admin.Client.GetFromJsonAsync<JsonElement>("/api/audience/settings", ct);
        using HttpResponseMessage freeChange = await admin.PutKeyAsync("external_id");

        // Assert
        Assert.Equal(HttpStatusCode.OK, sameKey.StatusCode);
        Assert.True((await admin.ReadAsync(sameKey)).GetProperty("isLocked").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, lockedChange.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byCreator.StatusCode);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.False(afterRemove.GetProperty("isLocked").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, freeChange.StatusCode);
    }

    [Fact]
    public async Task UpdatePerson_ValuesByDataType_RejectsWrongTypeAndSettlesParallelCells()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        long propertyId = await api.CreatePropertyAsync(formId, new { name = "Age", dataType = "number" });
        using HttpResponseMessage added = await api.AddPersonAsync(formId, "values@example.com");
        string personUrl = $"/api/forms/{formId}/audience/people/{IdOf(await api.ReadAsync(added), "membershipId")}";

        // Act
        using HttpResponseMessage wrongType = await api.PutValueAsync(personUrl, propertyId, "abc");
        HttpResponseMessage[] parallel = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(i => api.PutValueAsync(personUrl, propertyId, $"{40 + i}")));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        Assert.All(parallel, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        DisposeAll(parallel);
    }

    [Fact]
    public async Task CreateProperty_ChoiceSettings_CheckedAgainstDataType()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        string url = $"/api/forms/{formId}/audience/properties";

        // Act
        using HttpResponseMessage noChoices = await api.PostAsync(
            url, new { name = "Plan", dataType = "single_choice" });
        using HttpResponseMessage choicesOnText = await api.PostAsync(
            url, new { name = "Note", dataType = "text", choicesJson = """["a"]""" });
        using HttpResponseMessage valid = await api.PostAsync(
            url, new { name = "Tier", dataType = "single_choice", choicesJson = """["basic","pro"]""" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, noChoices.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, choicesOnText.StatusCode);
        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
    }

    [Fact]
    public async Task DeleteProperty_WithValues_RemovesThemFromThePeopleList()
    {
        // Arrange
        AudienceApi api = await AdminAsync();
        long formId = await api.CreateFormAsync();
        long propertyId = await api.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        using HttpResponseMessage added = await api.PostAsync(
            $"/api/forms/{formId}/audience/people",
            new { identifier = "city@example.com", values = new Dictionary<string, string> { [$"{propertyId}"] = "Sofia" } });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        // Act
        using HttpResponseMessage deleted = await api.Client.DeleteAsync(
            $"/api/forms/{formId}/audience/properties/{propertyId}", api.CancellationToken);
        JsonElement page = await api.Client.GetFromJsonAsync<JsonElement>(
            $"/api/forms/{formId}/audience/people", api.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        JsonElement person = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Empty(person.GetProperty("values").EnumerateObject());
    }

    private async Task<AudienceApi> AdminAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        return new AudienceApi(await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct), ct);
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
    }
}

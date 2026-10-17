using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests.FeatureFlows.Reporting;

/// <summary>
/// The form's saved submissions grid view over HTTP (#1257), against the real tenant middleware, permissions and the
/// database's unique index and revision check, on both providers.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class SubmissionViewFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const string Definition = """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"},{"type":"text","name":"q2"}]}]}""";
    private const string Settings =
        """{"v":1,"columns":[{"key":"q2","width":180},{"key":"q1","visible":false},{"key":"createdAt"}],"pageSize":25,"sort":{"key":"createdAt","direction":"desc"}}""";

    [Fact]
    public async Task A_saved_view_is_returned_to_every_user_of_the_form()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        using var creator = await world.AsAsync(TestPersona.Creator, cancellationToken: ct);
        var formId = await CreateCompiledFormAsync(admin, ct);

        // Act
        using var before = await admin.GetAsync(ViewUri(formId), ct);
        using var saved = await admin.PutAsJsonAsync(ViewUri(formId), SaveBody(0, Settings), ct);
        using var read = await creator.GetAsync(ViewUri(formId), ct);

        // Assert
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        var defaults = await BodyAsync(before, ct);
        defaults.Revision().Should().Be(0);
        defaults.GetProperty("settings").GetProperty("columns").GetArrayLength().Should().Be(0);

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BodyAsync(saved, ct)).Revision().Should().Be(1);

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        var view = await BodyAsync(read, ct);
        view.Revision().Should().Be(1);
        JsonElement.DeepEquals(view.GetProperty("settings"), Parse(Settings)).Should().BeTrue();
    }

    [Fact]
    public async Task A_save_from_an_old_revision_is_a_conflict()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        var formId = await CreateFormAsync(client, ct);
        using var first = await client.PutAsJsonAsync(ViewUri(formId), SaveBody(0, Settings), ct);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        using var secondCreate = await client.PutAsJsonAsync(ViewUri(formId), SaveBody(0, Settings), ct);
        using var update = await client.PutAsJsonAsync(ViewUri(formId), SaveBody(1, """{"v":1,"columns":[],"pageSize":50}"""), ct);
        using var staleUpdate = await client.PutAsJsonAsync(ViewUri(formId), SaveBody(1, Settings), ct);
        using var futureUpdate = await client.PutAsJsonAsync(ViewUri(formId), SaveBody(9, Settings), ct);

        // Assert
        secondCreate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BodyAsync(update, ct)).Revision().Should().Be(2);
        staleUpdate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        staleUpdate.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        futureUpdate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var read = await client.GetAsync(ViewUri(formId), ct);
        var view = await BodyAsync(read, ct);
        view.Revision().Should().Be(2);
        view.GetProperty("settings").GetProperty("pageSize").GetInt32().Should().Be(50);
    }

    [Fact]
    public async Task Malformed_or_oversized_settings_are_refused()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        var formId = await CreateFormAsync(client, ct);
        var oversizedColumns = string.Join(
            ',',
            Enumerable.Range(0, 8_000).Select(index => $$"""{"key":"question_{{index}}","width":120}"""));

        // Act
        using var malformed = await client.PutAsJsonAsync(
            ViewUri(formId), SaveBody(0, """{"v":1,"columns":[{"key":"q1","width":"wide"}]}"""), ct);
        using var oversized = await client.PutAsJsonAsync(
            ViewUri(formId), SaveBody(0, $$"""{"v":1,"columns":[{{oversizedColumns}}]}"""), ct);

        // Assert
        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BodyAsync(malformed, ct)).GetProperty("detail").GetString()
            .Should().Be("Column \"q1\" must have a \"width\" from 1 to 4000.");
        oversized.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BodyAsync(oversized, ct)).GetProperty("detail").GetString().Should().Contain("cannot be longer than");

        using var read = await client.GetAsync(ViewUri(formId), ct);
        (await BodyAsync(read, ct)).Revision().Should().Be(0, "nothing was saved");
    }

    [Fact]
    public async Task Another_tenant_cannot_read_or_save_the_view()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword }, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 0, cancellationToken: ct);
        using var other = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: ct);
        var formId = await CreateFormAsync(owner, ct);
        using var saved = await owner.PutAsJsonAsync(ViewUri(formId), SaveBody(0, Settings), ct);
        saved.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        using var foreignRead = await other.GetAsync(ViewUri(formId), ct);
        using var foreignUpdate = await other.PutAsJsonAsync(ViewUri(formId), SaveBody(1, SubmissionViewDefaultJson), ct);
        using var foreignCreate = await other.PutAsJsonAsync(ViewUri(formId), SaveBody(0, SubmissionViewDefaultJson), ct);

        // Assert
        foreignRead.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreignUpdate.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreignCreate.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var read = await owner.GetAsync(ViewUri(formId), ct);
        (await BodyAsync(read, ct)).Revision().Should().Be(1);
        (await CountViewsAsync(formId, ct)).Should().Be(1, "the other tenant created no view of its own");
    }

    [Fact]
    // The view's removal itself is the form.deleted sync's job (SyncFormDeletionOutboxHandlerIntegrationTests).
    // ReportingWebHost keeps its outbox relay closed, so nothing here runs that sync.
    public async Task Deleting_the_form_hides_its_view()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        var formId = await CreateFormAsync(client, ct);
        using var saved = await client.PutAsJsonAsync(ViewUri(formId), SaveBody(0, Settings), ct);
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountViewsAsync(formId, ct)).Should().Be(1);

        // Act
        using var deleted = await client.DeleteAsync(new Uri($"/api/forms/{formId}", UriKind.Relative), ct);

        // Assert
        deleted.IsSuccessStatusCode.Should().BeTrue();
        using var read = await client.GetAsync(ViewUri(formId), ct);
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private const string SubmissionViewDefaultJson = """{"v":1,"columns":[]}""";

    private static Uri ViewUri(long formId) => new($"/api/forms/{formId}/reporting/submission-view", UriKind.Relative);

    private static object SaveBody(long revision, string settings) => new { revision, settings = Parse(settings) };

    private static async Task<long> CreateFormAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/forms",
            new { name = $"grid-view-{Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = Definition },
            ct);
        response.EnsureSuccessStatusCode();
        return long.Parse((await BodyAsync(response, ct)).GetProperty("id").GetString()!);
    }

    // The schema decides which saved columns a read keeps, so compile it rather than wait for the outbox.
    private static async Task<long> CreateCompiledFormAsync(HttpClient client, CancellationToken ct)
    {
        var formId = await CreateFormAsync(client, ct);
        using var compiled = await client.PostAsJsonAsync($"/api/forms/{formId}/reporting/compile-schema", new { }, ct);
        compiled.EnsureSuccessStatusCode();
        return formId;
    }

    private async Task<int> CountViewsAsync(long formId, CancellationToken ct)
    {
        await using var context = ReportingTestSchema.CreateContext(
            fixture.Database.ConnectionString, fixture.Provider, IntegrationTenantContext.Bypass);
        return await context.SubmissionViews.IgnoreQueryFilters().CountAsync(view => view.FormId == formId, ct);
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response, CancellationToken ct) =>
        Parse(await response.Content.ReadAsStringAsync(ct));

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

internal static class SubmissionViewJson
{
    // Longs travel as JSON strings on this API.
    public static long Revision(this JsonElement view) => long.Parse(view.GetProperty("revision").GetString()!);
}

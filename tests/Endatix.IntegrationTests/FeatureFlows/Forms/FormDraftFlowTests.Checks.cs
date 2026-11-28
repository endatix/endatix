using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Endatix.Core.UseCases.FormDrafts;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.IntegrationTests.FeatureFlows.Forms;

public sealed partial class FormDraftFlowTests
{
    [Fact]
    public async Task Repeated_checks_report_each_duplicate_generated_key_in_the_saved_draft()
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        var jsonData = """
            {"elements":[{"type":"matrix","name":"m","rows":["r","r"]},{"type":"text","name":"x"},
                         {"type":"text","name":"x"},{"type":"missingWidget","name":"unsupported"}]}
            """;
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var saved = await editor.PutAsJsonAsync(form + "/draft", new { jsonData }, Cancellation);
        saved.EnsureSuccessStatusCode();
        var draft = await saved.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        editor.DefaultRequestHeaders.IfMatch.Clear();

        // Act
        var first = await CheckAsync(editor, form);
        var second = await CheckAsync(editor, form);

        // Assert
        first.GetProperty("draftVersion").GetString().Should().Be("2");
        first.GetProperty("contentHash").GetString().Should().Be(draft.GetProperty("contentHash").GetString());
        first.GetProperty("outcome").GetString().Should().Be("block");
        var findings = first.GetProperty("findings").EnumerateArray().ToArray();
        findings.Count(f => f.GetProperty("code").GetString() == "column/duplicate-key").Should().Be(2);
        findings.Count(f => f.GetProperty("code").GetString() == "element/unknown-type").Should().Be(1);
        first.GetRawText().Should().Be(second.GetRawText());
    }

    private static async Task<JsonElement> CheckAsync(HttpClient editor, string form)
    {
        using var response = await editor.PostAsJsonAsync(form + "/draft/checks", new { }, Cancellation);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
    }

    [Fact]
    public async Task Check_resolves_the_tenants_component_json_name_and_keeps_nested_locations()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, Cancellation);
        using var owner = await world.AsAsync(TestPersona.Creator, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: Cancellation);
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CustomQuestions.Add(new(world.Tenants[0].Id, "Display name", """{"name":"own","title":"Own","questionJSON":{"type":"text"}}"""));
        db.CustomQuestions.Add(new(world.Tenants[1].Id, "Other display", """{"name":"other","title":"Other","questionJSON":{"type":"text"}}"""));
        await db.SaveChangesAsync(Cancellation);
        var form = await CreateFormAsync(owner);
        var jsonData = """
            {"pages":[{"name":"p/1","elements":[{"type":"paneldynamic","name":"p~2","templateElements":[
                {"type":"own","name":"a"},{"type":"richtexteditor","name":"b"},{"type":"audiorecorder","name":"c"},
                {"type":"dragcategorize","name":"d"},{"type":"imagemap","name":"e"},{"type":"other","name":"f"}]}]}]}
            """;
        owner.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var saved = await owner.PutAsJsonAsync(form + "/draft", new { jsonData }, Cancellation);
        saved.EnsureSuccessStatusCode();
        owner.DefaultRequestHeaders.IfMatch.Clear();

        // Act
        var report = await CheckAsync(owner, form);

        // Assert
        var findings = report.GetProperty("findings").EnumerateArray();
        var unknown = findings.Single(f => f.GetProperty("code").GetString() == "element/unknown-type");
        unknown.GetProperty("path").GetProperty("element").GetString().Should()
            .Be("/pages/name:p~11/elements/name:p~02/templateElements/name:f");
        report.GetProperty("outcome").GetString().Should().Be("block");
        (await db.FormDraftOperations.CountAsync(row => row.FormId == FormId(form), Cancellation)).Should().Be(2);
    }

    [Fact]
    public async Task Missing_checks_return_retryable_problem_only_after_tenant_and_upgrade_admission()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, Cancellation);
        using var owner = await world.AsAsync(TestPersona.Creator, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: Cancellation);
        using var other = await world.AsAsync(TestPersona.Creator, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: Cancellation);
        var form = await CreateFormAsync(owner);
        await using var factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        await using var custom = factory.WithWebHostBuilder(host =>
            host.ConfigureTestServices(services => services.RemoveAll<IFormDefinitionCheck>()));
        using var client = custom.CreateClient();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Act
        var anonymous = await ReadCheckProblemAsync(client, form);
        client.DefaultRequestHeaders.Authorization = other.DefaultRequestHeaders.Authorization;
        var hidden = await ReadCheckProblemAsync(client, form);
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        var unavailable = await ReadCheckProblemAsync(client, form);
        var id = FormId(form);
        var active = await db.FormDefinitions.Where(row => row.FormId == id).Select(row => row.Id).SingleAsync(Cancellation);
        db.Add(new LegacyDraftUpgrade(id, active));
        await db.SaveChangesAsync(Cancellation);
        var pending = await ReadCheckProblemAsync(client, form);

        // Assert
        anonymous.Status.Should().Be(HttpStatusCode.Unauthorized);
        hidden.Status.Should().Be(HttpStatusCode.NotFound);
        unavailable.Should().Be((HttpStatusCode.ServiceUnavailable, "Required draft checks are unavailable. Retry after the host configuration is restored."));
        pending.Status.Should().Be(HttpStatusCode.ServiceUnavailable);
        pending.Detail.Should().Contain("upgrade is pending");
        (await db.FormDrafts.AnyAsync(d => d.FormId == id, Cancellation)).Should().BeFalse();
        (await db.FormDraftOperations.AnyAsync(d => d.FormId == id, Cancellation)).Should().BeFalse();
    }

    private static async Task<(HttpStatusCode Status, string? Detail)> ReadCheckProblemAsync(HttpClient client, string form)
    {
        using var response = await client.PostAsJsonAsync(form + "/draft/checks", new { }, Cancellation);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return (response.StatusCode, null);
        }
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        return (response.StatusCode, body.GetProperty("detail").GetString());
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Forms;

public sealed partial class FormDraftFlowTests
{
    private const string DIFF = "/draft/diff?against=published";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task First_publication_has_no_comparison_even_after_a_draft_edit(bool edited)
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, Cancellation);
        using var editor = await world.AsAsync(TestPersona.Creator, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: Cancellation);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = Form.Create(new FormCreateArgs(TenantId: world.Tenants[0].Id, Name: "First version"));
        db.Forms.Add(owner);
        await db.SaveChangesAsync(Cancellation);
        var form = $"/api/forms/{owner.Id}";
        if (edited)
        {
            await EditDraftAsync(editor, form);
        }

        // Act
        using var response = await editor.GetAsync(form + DIFF, Cancellation);

        // Assert
        response.EnsureSuccessStatusCode();
        response.Headers.ETag!.Tag.Should().Be(edited ? "\"2\"" : "\"0\"");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        body.GetProperty("publishedNumber").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("changes").GetArrayLength().Should().Be(0);
        (await db.FormDrafts.CountAsync(x => x.FormId == owner.Id, Cancellation)).Should().Be(edited ? 1 : 0);
        (await db.FormDraftOperations.CountAsync(x => x.FormId == owner.Id, Cancellation)).Should().Be(edited ? 2 : 0);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(8L, 8)]
    public async Task Identical_documents_return_the_live_number_even_for_an_unnumbered_definition(long? storedNumber, int number)
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var definition = await db.FormDefinitions.SingleAsync(x => x.FormId == FormId(form), Cancellation);
        db.Entry(definition).Property(x => x.Number).CurrentValue = storedNumber;
        await db.SaveChangesAsync(Cancellation);

        // Act
        var body = await editor.GetFromJsonAsync<JsonElement>(form + DIFF, Cancellation);

        // Assert
        body.GetProperty("publishedNumber").GetInt32().Should().Be(number);
        body.GetProperty("changes").GetArrayLength().Should().Be(0);
        (await db.FormDrafts.AnyAsync(x => x.FormId == definition.FormId, Cancellation)).Should().BeFalse();
    }

    [Fact]
    public async Task Diff_returns_actual_values_even_with_a_matching_draft_tag()
    {
        // Arrange
        using var editor = await EditorAsync();
        var form = await CreateFormAsync(editor);
        await EditDraftAsync(editor, form);
        editor.DefaultRequestHeaders.IfNoneMatch.ParseAdd("\"2\"");

        // Act
        using var response = await editor.GetAsync(form + DIFF, Cancellation);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.ETag!.Tag.Should().Be("\"2\"");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var change = body.GetProperty("changes").EnumerateArray().Single();
        change.GetProperty("path").GetString().Should().Be("/title");
        change.GetProperty("before").GetString().Should().Be("Live");
        change.GetProperty("after").GetString().Should().Be("Edited");
        body.GetProperty("publishedJson").GetString().Should().Be("""{"pages":[],"title":"Live"}""");
        body.GetProperty("draftJson").GetString().Should().Be("""{"pages":[],"title":"Edited"}""");
    }

    [Theory]
    [InlineData("?against=3", HttpStatusCode.BadRequest)]
    [InlineData("", HttpStatusCode.BadRequest)]
    [InlineData("?against=published", HttpStatusCode.NotFound)]
    public async Task Diff_rejects_unknown_forms_or_unsupported_targets(string query, HttpStatusCode expected)
    {
        using var editor = await EditorAsync();
        using var response = await editor.GetAsync("/api/forms/1/draft/diff" + query, Cancellation);
        response.StatusCode.Should().Be(expected);
    }

    private static long FormId(string path) => long.Parse(path.Split('/')[^1], System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Diff_hides_another_tenants_pending_draft_and_refuses_anonymous_reads()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, Cancellation);
        using var editor = await world.AsAsync(TestPersona.Creator, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: Cancellation);
        using var other = await world.AsAsync(TestPersona.Creator, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: Cancellation);
        using var anonymous = world.AnonymousClient();
        var form = await CreateFormAsync(editor);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = FormId(form);
        var definitionId = await db.FormDefinitions.Where(x => x.FormId == id).Select(x => x.Id).SingleAsync(Cancellation);
        db.Add(new LegacyDraftUpgrade(id, definitionId));
        await db.SaveChangesAsync(Cancellation);

        // Act
        using var pending = await editor.GetAsync(form + DIFF, Cancellation);
        using var hidden = await other.GetAsync(form + DIFF, Cancellation);
        using var unauthenticated = await anonymous.GetAsync(form + DIFF, Cancellation);

        // Assert
        pending.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unauthenticated.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task EditDraftAsync(HttpClient editor, string form)
    {
        editor.DefaultRequestHeaders.IfMatch.ParseAdd("\"0\"");
        using var response = await editor.PutAsJsonAsync($"{form}/draft", new { jsonData = EDITED }, Cancellation);
        response.EnsureSuccessStatusCode();
        editor.DefaultRequestHeaders.IfMatch.Clear();
    }
}

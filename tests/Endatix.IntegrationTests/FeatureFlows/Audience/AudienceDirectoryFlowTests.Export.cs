using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The export flow end to end over HTTP: the all-questions-personalized form, its audience imported from CSV, two
/// responses created on behalf of panel members, a backfill, then the CSV and codebook exports before and after an
/// operator renames and reorders a property.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    private const string AudienceCsv = "all-questions-personalized-audience.csv";

    // Named so each slugs to the CSV column it maps: "Full name" → full_name.
    private static readonly (string Name, string DataType, string? Choices)[] _audienceProperties =
    [
        ("Full name", "text", null),
        ("Age", "number", null),
        ("City", "text", null),
        ("Panel member", "boolean", null),
        ("Joined", "date", null),
        ("Last seen", "date_time", null),
        ("Tags", "multiple_choice", """["sports","music","travel","food"]"""),
    ];

    [Fact]
    public async Task Exports_carry_imported_audience_values_and_follow_property_renames_and_order()
    {
        // Arrange
        var world = await WorldAsync();
        var api = await AsAsync(world, TestPersona.TenantAdmin);
        await EnsureTenantSettingsAsync(world);
        using var key = await api.PutKeyAsync("external_id");
        var formId = await api.CreateFormAsync(definition: PersonalizedDefinition());
        var properties = await CreateAudiencePropertiesAsync(api, formId);
        await ImportAudienceAsync(api, formId);
        await CompletedOnBehalfAsync(api, formId, "AQ-000001-01");
        await CompletedOnBehalfAsync(api, formId, "AQ-000002-01");
        await ExpectOkAsync(api.PostAsync($"/api/forms/{formId}/submissions/backfill", new { }));

        // Act
        var csvBefore = await ExportAsync(api, formId, "csv");
        await ExpectOkAsync(api.Client.PatchAsJsonAsync(
            $"/api/forms/{formId}/audience/properties/{properties["city"]}",
            new { name = "Home city", sortOrder = -1 },
            api.CancellationToken));
        var csvAfter = await ExportAsync(api, formId, "csv");
        using var codebook = JsonDocument.Parse(await ExportAsync(api, formId, "codebook"));

        // Assert
        Assert.Equal(
            ["AudienceIdentifier", "audience_full_name", "audience_age", "audience_city", "audience_panel_member", "audience_joined", "audience_last_seen", "audience_tags"],
            AudienceHeaders(csvBefore));
        Assert.Contains("John Smith", csvBefore);
        Assert.Contains("AQ-000002-01", csvBefore);
        Assert.Equal(
            ["AudienceIdentifier", "audience_city", "audience_full_name", "audience_age", "audience_panel_member", "audience_joined", "audience_last_seen", "audience_tags"],
            AudienceHeaders(csvAfter));
        var root = codebook.RootElement;
        Assert.Equal(
            "Home city",
            root.GetProperty("columns").GetProperty("audience_city").GetProperty("title").GetProperty("default").GetString());
        Assert.Equal("city", root.GetProperty("personalization").GetProperty("variables").EnumerateObject().First().Name);
    }

    // Completing a submission raises the Slack handler, which needs the tenant's settings row.
    private static async Task EnsureTenantSettingsAsync(IntegrationTestWorld world)
    {
        await using var scope = world.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = world.Tenants[0].Id;
        if (!await db.TenantSettings.AnyAsync(row => row.TenantId == tenantId, TestContext.Current.CancellationToken))
        {
            db.TenantSettings.Add(new TenantSettings(tenantId));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private static string PersonalizedDefinition() =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "AllQuestionsPersonalized",
            "all-questions-personalized-definition.json"));

    private static async Task<Dictionary<string, long>> CreateAudiencePropertiesAsync(AudienceApi api, long formId)
    {
        Dictionary<string, long> ids = [];
        foreach (var (name, dataType, choices) in _audienceProperties)
        {
            var id = await api.CreatePropertyAsync(formId, new { name, dataType, choicesJson = choices });
            ids[name.ToLowerInvariant().Replace(' ', '_')] = id;
        }

        return ids;
    }

    // Every column but the identifier maps by name; tags__<choice> columns are read by the API from their names.
    private static async Task ImportAudienceAsync(AudienceApi api, long formId)
    {
        var csv = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "AllQuestionsPersonalized", AudienceCsv));
        var columns = _audienceProperties
            .Where(property => property.Choices is null)
            .Select(property => property.Name.ToLowerInvariant().Replace(' ', '_'))
            .ToDictionary(variable => variable, variable => variable);
        using var response = await api.PostAsync(
            $"/api/forms/{formId}/audience/import",
            new { csvText = csv, identifierColumn = "external_id", fileName = AudienceCsv, propertyColumns = columns });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await api.ReadAsync(response);
        Assert.Equal(20, result.GetProperty("createdCount").GetInt32());
        Assert.Equal(0, result.GetProperty("rejectedCount").GetInt32());
    }

    private static async Task CompletedOnBehalfAsync(AudienceApi api, long formId, string externalId)
    {
        using var response = await api.PostAsync(
            $"/api/forms/{formId}/submissions/onbehalf",
            new
            {
                isComplete = true,
                jsonData = "{}",
                submitter = new { externalSubjectId = externalId, displayId = externalId, authProvider = "Integration" },
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(JsonValueKind.Object, (await api.ReadAsync(response)).GetProperty("personalization").ValueKind);
    }

    private static async Task<string> ExportAsync(AudienceApi api, long formId, string wireKey)
    {
        var formats = await api.Client.GetFromJsonAsync<JsonElement>("/api/settings/export-formats", api.CancellationToken);
        var formatId = IdOf(formats.EnumerateArray().First(format => format.GetProperty("wireKey").GetString() == wireKey), "id");
        using var response = await api.PostAsync(
            $"/api/forms/{formId}/submissions/export",
            new { exportFormatId = formatId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(api.CancellationToken);
    }

    private static List<string> AudienceHeaders(string csv) =>
        csv.Split('\n', 2)[0].TrimEnd('\r').Split(',')
            .Select(header => header.Trim('"'))
            .Where(header => header.StartsWith("Audience", StringComparison.OrdinalIgnoreCase))
            .ToList();
}

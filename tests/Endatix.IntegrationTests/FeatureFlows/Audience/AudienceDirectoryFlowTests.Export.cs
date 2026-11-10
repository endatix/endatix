using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The export flow end to end over HTTP: the all-questions-personalized form, its audience imported from CSV, two
/// responses created on behalf of panel members, a backfill, then the CSV and codebook exports before and after an
/// operator renames and reorders a property. Runs on <see cref="ReportingWebHost"/>, which flattens through the backfill
/// alone.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    private const string AudienceCsv = "all-questions-personalized-audience.csv";

    // Named so each slugs to the CSV column it maps: "Full name" → full_name.
    private static readonly (string Name, string DataType, string? Choices)[] _audienceProperties =
    [
        ("Full name", "text", null),
        ("Age", "number", null),
        ("Hometown", "text", null),
        ("Panel member", "boolean", null),
        ("Joined", "date", null),
        ("Last seen", "date_time", null),
        ("Tags", "multiple_choice", """["sports","music","travel","food"]"""),
    ];

    [Fact]
    public async Task Exports_carry_imported_audience_values_and_follow_property_renames_and_order()
    {
        // Arrange
        await using ReportingWebHost host = new(fixture);
        var world = await WorldAsync(host);
        var api = await AsAsync(world, TestPersona.TenantAdmin);
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
            $"/api/forms/{formId}/audience/properties/{properties["hometown"]}",
            new { name = "Home city", sortOrder = -1 },
            api.CancellationToken));
        var csvAfter = await ExportAsync(api, formId, "csv");
        using var codebook = JsonDocument.Parse(await ExportAsync(api, formId, "codebook"));

        // Assert
        Assert.Equal(
            ["ParticipantIdentifier", "participant_full_name", "participant_age", "participant_hometown", "participant_panel_member", "participant_joined", "participant_last_seen", "participant_tags"],
            AudienceHeaders(csvBefore));
        Assert.Contains("John Smith", csvBefore);
        Assert.Contains("AQ-000002-01", csvBefore);
        Assert.Equal(
            ["ParticipantIdentifier", "participant_hometown", "participant_full_name", "participant_age", "participant_panel_member", "participant_joined", "participant_last_seen", "participant_tags"],
            AudienceHeaders(csvAfter));
        var root = codebook.RootElement;
        Assert.Equal(
            "Home city",
            root.GetProperty("columns").GetProperty("participant_hometown").GetProperty("title").GetProperty("default").GetString());
        Assert.Equal("hometown", root.GetProperty("participants").GetProperty("variables").EnumerateObject().First().Name);
    }

    [Fact]
    public async Task A_reflatten_moves_a_canary_form_from_audience_keys_to_participant_keys()
    {
        // Arrange: the form was flattened by a canary that keyed participant columns as audience_* (#1282), and a
        // response came in after the upgrade, before anyone rebuilt it.
        await using ReportingWebHost host = new(fixture);
        var world = await WorldAsync(host);
        var api = await AsAsync(world, TestPersona.TenantAdmin);
        using var key = await api.PutKeyAsync("external_id");
        var formId = await api.CreateFormAsync(definition: PersonalizedDefinition());
        await CreateAudiencePropertiesAsync(api, formId);
        await ImportAudienceAsync(api, formId);
        await CompletedOnBehalfAsync(api, formId, "AQ-000001-01");
        await ExpectOkAsync(api.PostAsync($"/api/forms/{formId}/submissions/backfill", new { }));
        await KeyAsInCanaryAsync(world.Services, formId);
        await CompletedOnBehalfAsync(api, formId, "AQ-000002-01");
        await ExpectOkAsync(api.PostAsync($"/api/forms/{formId}/submissions/backfill", new { }));
        var csvBefore = await ExportAsync(api, formId, "csv");
        using var codebookBefore = JsonDocument.Parse(await ExportAsync(api, formId, "codebook"));

        // Act
        await ExpectOkAsync(api.PostAsync($"/api/forms/{formId}/reporting/compile-schema", new { replace = true }));
        await ExpectOkAsync(api.PostAsync($"/api/forms/{formId}/submissions/backfill", new { }));
        var csvAfter = await ExportAsync(api, formId, "csv");
        using var codebook = JsonDocument.Parse(await ExportAsync(api, formId, "codebook"));

        // Assert
        AssertCanaryExportBeforeTheRebuild(csvBefore, codebookBefore.RootElement);
        AssertParticipantExport(csvAfter, codebook.RootElement);
    }

    // A response flattened onto the canary schema fills participant_* columns; the participants block names no
    // audience_ key.
    private static void AssertCanaryExportBeforeTheRebuild(string csv, JsonElement codebook)
    {
        Assert.Contains("audience_full_name", AudienceHeaders(csv, "audience_"));
        Assert.Contains("participant_age", AudienceHeaders(csv));
        Assert.Contains("AQ-000002-01", csv);
        var variables = codebook.GetProperty("participants").GetProperty("variables");
        Assert.Equal("participant_age", variables.GetProperty("age").GetString());
        Assert.DoesNotContain(
            variables.EnumerateObject(),
            variable => variable.Value.GetString()!.StartsWith("audience_", StringComparison.Ordinal));
    }

    private static void AssertParticipantExport(string csv, JsonElement codebook)
    {
        Assert.Equal(
            ["ParticipantIdentifier", "participant_full_name", "participant_age", "participant_hometown", "participant_panel_member", "participant_joined", "participant_last_seen", "participant_tags"],
            AudienceHeaders(csv));
        Assert.Empty(AudienceHeaders(csv, "audience"));
        Assert.Contains("John Smith", csv);
        var block = codebook.GetProperty("participants");
        Assert.Equal("ParticipantIdentifier", block.GetProperty("identifier").GetString());
        Assert.Equal("Participants", block.GetProperty("title").GetProperty("default").GetString());
        Assert.False(codebook.TryGetProperty("personalization", out _));
        var hometown = codebook.GetProperty("columns").GetProperty("participant_hometown");
        Assert.Equal("participant", hometown.GetProperty("surveyJsType").GetString());
        Assert.Equal("participant", hometown.GetProperty("exportShape").GetString());
        Assert.DoesNotContain(
            codebook.GetProperty("columns").EnumerateObject(),
            column => column.Name.StartsWith("audience", StringComparison.OrdinalIgnoreCase));
    }

    // Rewrites the stored schema and flattened rows to the keys a canary before #1282 wrote.
    private static async Task KeyAsInCanaryAsync(IServiceProvider services, long formId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var reporting = scope.ServiceProvider.GetRequiredService<IReportingDbContext>();
        var schema = await reporting.FormSchemas.AsNoTracking().SingleAsync(row => row.FormId == formId, cancellationToken);
        var (flatteningMap, codebook) = (AsInCanary(schema.FlatteningMap), AsInCanary(schema.Codebook));
        await reporting.FormSchemas
            .Where(row => row.FormId == formId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.FlatteningMap, flatteningMap)
                    .SetProperty(row => row.Codebook, codebook),
                cancellationToken);
        var rows = await reporting.FlattenedSubmissions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.FormId == formId)
            .Select(row => new { row.SubmissionId, row.DataJson })
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            var dataJson = AsInCanary(row.DataJson!);
            await reporting.FlattenedSubmissions
                .IgnoreQueryFilters()
                .Where(flattened => flattened.SubmissionId == row.SubmissionId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(flattened => flattened.DataJson, dataJson), cancellationToken);
        }

        Assert.Single(rows);
    }

    private static string AsInCanary(string json) =>
        json.Replace("ParticipantIdentifier", "AudienceIdentifier", StringComparison.Ordinal)
            .Replace("participant_", "audience_", StringComparison.Ordinal)
            .Replace("\"participant\"", "\"personalization\"", StringComparison.Ordinal);

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

    private static List<string> AudienceHeaders(string csv, string prefix = "Participant") =>
        csv.Split('\n', 2)[0].TrimEnd('\r').Split(',')
            .Select(header => header.Trim('"'))
            .Where(header => header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
}

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests.FeatureFlows.DataLists;

/// <summary>
/// End-to-end flow for data list import/export with format negotiation.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class DataListTranslationsCsvFlowTests
{
    private const string SeedPassword = "Password123!";
    private const string CsvMediaType = "text/csv";

    private readonly EndatixIntegrationWebHostFixture _fixture;

    public DataListTranslationsCsvFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ImportExport_ValidTranslationsCsv_RoundTripsContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = await CreateAuthenticatedClientAsync(ct);
        long dataListId = await CreateDataListWithSpanishAsync(client, ct);
        const string csv = "value,default,es\r\napple,Apple,Manzana\r\nbanana,Banana,\r\n";

        HttpResponseMessage importResponse = await ImportCsvAsync(client, dataListId, csv, ct);
        HttpResponseMessage exportResponse = await ExportAsync(client, dataListId, "csv", ct);

        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        exportResponse.Content.Headers.ContentType!.MediaType.Should().Be(CsvMediaType);
        (await exportResponse.Content.ReadAsStringAsync(ct)).Should().Be(csv);
    }

    [Fact]
    public async Task Import_UnknownLocaleColumn_ReturnsBadRequest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = await CreateAuthenticatedClientAsync(ct);
        long dataListId = await CreateDataListWithSpanishAsync(client, ct);

        HttpResponseMessage response = await ImportCsvAsync(
            client,
            dataListId,
            "value,default,fr\r\napple,Apple,Pomme\r\n",
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain("fr");
    }

    [Fact]
    public async Task Import_EnsureLocales_AddsCulturesAndImports()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = await CreateAuthenticatedClientAsync(ct);
        long dataListId = await CreateBareDataListAsync(client, ct);
        const string csv = "value,default,fr,es\r\napple,Apple,Pomme,Manzana\r\n";

        HttpResponseMessage importResponse = await ImportCsvAsync(
            client,
            dataListId,
            csv,
            ct,
            ensureLocales: ["fr", "es"]);

        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument details = JsonDocument.Parse(await importResponse.Content.ReadAsStringAsync(ct));
        details.RootElement.GetProperty("availableLocales")
            .EnumerateArray()
            .Select(e => e.GetString())
            .Should()
            .BeEquivalentTo(["fr", "es"]);

        HttpResponseMessage exportResponse = await ExportAsync(client, dataListId, "csv", ct);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await exportResponse.Content.ReadAsStringAsync(ct)).Should().Be(csv);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(CancellationToken ct)
    {
        IntegrationTestWorld world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword },
            ct);

        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
    }

    private static async Task<long> CreateBareDataListAsync(HttpClient client, CancellationToken ct)
    {
        HttpResponseMessage created = await client.PostAsJsonAsync(
            new Uri("/api/data-lists", UriKind.Relative),
            new { name = $"Fruits-{Guid.NewGuid():N}" },
            ct);
        created.EnsureSuccessStatusCode();

        using JsonDocument document = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));
        JsonElement id = document.RootElement.GetProperty("id");
        return id.ValueKind == JsonValueKind.String
            ? long.Parse(id.GetString()!, CultureInfo.InvariantCulture)
            : id.GetInt64();
    }

    private static async Task<long> CreateDataListWithSpanishAsync(HttpClient client, CancellationToken ct)
    {
        long dataListId = await CreateBareDataListAsync(client, ct);

        HttpResponseMessage localeAdded = await client.PostAsJsonAsync(
            new Uri($"/api/data-lists/{dataListId}/locales", UriKind.Relative),
            new { locale = "es" },
            ct);
        localeAdded.EnsureSuccessStatusCode();

        return dataListId;
    }

    private static async Task<HttpResponseMessage> ImportCsvAsync(
        HttpClient client,
        long dataListId,
        string csv,
        CancellationToken ct,
        IReadOnlyList<string>? ensureLocales = null)
    {
        return await client.PutAsJsonAsync(
            new Uri($"/api/data-lists/{dataListId}/import", UriKind.Relative),
            new
            {
                format = "csv",
                csv,
                ensureLocales = ensureLocales ?? Array.Empty<string>()
            },
            ct);
    }

    private static async Task<HttpResponseMessage> ExportAsync(
        HttpClient client,
        long dataListId,
        string format,
        CancellationToken ct)
    {
        return await client.PostAsJsonAsync(
            new Uri($"/api/data-lists/{dataListId}/export", UriKind.Relative),
            new { format },
            ct);
    }
}

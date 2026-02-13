using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Exporting.Transformers;
using Endatix.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submissions;

public sealed class StorageUrlRewriteTransformerTests
{
    private const long FormId = 1465276700186116096L;
    private const long SubmissionId = 1470691429209604096L;
    private const string StorageUrl = "https://endatixstoragedev.blob.core.windows.net/secure-vault/s/1465276700186116096/1470691429209604096/1750353e-22d6-4561-b7ba-6fe7d8dc1ac8.jpg";
    private const string HubUrl = "https://hub.example.com";

    private static SubmissionExportRow Row(long formId = FormId, long submissionId = SubmissionId) =>
        new() { FormId = formId, Id = submissionId };

    private static StorageUrlRewriteTransformer CreateSut(string? hubUrl = HubUrl, bool withAzureConfig = true)
    {
        var hubOptions = Options.Create(new HubSettings { HubBaseUrl = hubUrl ?? string.Empty });
        var azureOptions = Options.Create(
            withAzureConfig
                ? new AzureBlobStorageProviderOptions
                {
                    HostName = "endatixstoragedev.blob.core.windows.net",
                    UserFilesContainerName = "secure-vault"
                }
                : new AzureBlobStorageProviderOptions());
        var logger = Substitute.For<ILogger<StorageUrlRewriteTransformer>>();
        return new StorageUrlRewriteTransformer(hubOptions, azureOptions, logger);
    }

    private static TransformationContext<SubmissionExportRow> Ctx(SubmissionExportRow? row = null) =>
        new(row ?? Row(), null, null);

    private static JsonNode? Lift(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            return JsonNode.Parse(element.GetRawText());
        }

        if (element.ValueKind is JsonValueKind.String)
        {
            var s = element.GetString() ?? string.Empty;
            var span = s.AsSpan().TrimStart();
            if (!span.IsEmpty && (span[0] == '{' || span[0] == '['))
            {
                try
                {
                    return JsonNode.Parse(s);
                }
                catch (JsonException)
                {
                    // fall through
                }
            }

            return JsonValue.Create(s);
        }

        return JsonNode.Parse(element.GetRawText());
    }

    [Fact]
    public void Transform_ReturnsValueUnchanged_WhenArrayWithNoStorageUrls()
    {
        var sut = CreateSut();
        using var doc = JsonDocument.Parse("""[{"name":"f.jpg","type":"image/jpeg","content":"data:image/jpeg;base64,/9j/4AAQ"}]""");
        var result = sut.Transform<SubmissionExportRow>(Lift(doc.RootElement), Ctx());
        Assert.NotNull(result);
        Assert.Contains("data:image/jpeg;base64,/9j/4AAQ", result.ToString());
    }

    [Fact]
    public void Transform_RewritesArray_WhenStorageUrlMatches()
    {
        var sut = CreateSut();
        var json = $$"""[{"name":"f.jpg","type":"image/jpeg","content":"{{StorageUrl}}"}]""";
        using var doc = JsonDocument.Parse(json);
        var result = sut.Transform<SubmissionExportRow>(Lift(doc.RootElement), Ctx());
        Assert.NotNull(result);
        Assert.Contains($"{HubUrl}/forms/{FormId}/submissions/{SubmissionId}/files/1750353e-22d6-4561-b7ba-6fe7d8dc1ac8.jpg", result.ToString());
        Assert.DoesNotContain("blob.core.windows.net", result.ToString());
    }

    [Fact]
    public void Transform_RewritesSingleObject_WhenStorageUrlMatches()
    {
        var sut = CreateSut();
        var json = $$"""{"name":"f.jpg","type":"image/jpeg","content":"{{StorageUrl}}"}""";
        using var doc = JsonDocument.Parse(json);
        var result = sut.Transform<SubmissionExportRow>(Lift(doc.RootElement), Ctx());
        Assert.NotNull(result);
        Assert.Contains($"{HubUrl}/forms/{FormId}/submissions/{SubmissionId}/files/1750353e-22d6-4561-b7ba-6fe7d8dc1ac8.jpg", result.ToString());
    }

    [Fact]
    public void Transform_RewritesStringifiedArray_WhenStorageUrlMatches()
    {
        var sut = CreateSut();
        var stringified = """[{"name":"option-4.jpg","type":"image/jpeg","content":"https://endatixstoragedev.blob.core.windows.net/secure-vault/s/1465276700186116096/1466449599089606656/3efcdbf0-159c-4a87-b02a-ec69216bb44d.jpg"}]""";
        var json = $"{{\"val\":{JsonSerializer.Serialize(stringified)}}}";
        using var doc = JsonDocument.Parse(json);
        var result = sut.Transform<SubmissionExportRow>(Lift(doc.RootElement.GetProperty("val")), Ctx(Row(1465276700186116096L, 1466449599089606656L)));
        Assert.NotNull(result);
        Assert.Contains("3efcdbf0-159c-4a87-b02a-ec69216bb44d.jpg", result.ToString());
        Assert.DoesNotContain("blob.core.windows.net", result.ToString());
    }

    [Fact]
    public void Transform_ReturnsValueUnchanged_WhenNumber()
    {
        var sut = CreateSut();
        using var doc = JsonDocument.Parse("123");
        var result = sut.Transform<SubmissionExportRow>(Lift(doc.RootElement), Ctx());
        Assert.NotNull(result);
        Assert.Equal("123", result.ToJsonString());
    }

    [Fact]
    public void Transform_ReturnsValueUnchanged_WhenNoHubUrl()
    {
        var sut = CreateSut(hubUrl: null);
        var json = $$"""[{"name":"f.jpg","content":"{{StorageUrl}}"}]""";
        using var doc = JsonDocument.Parse(json);
        var result = sut.Transform<SubmissionExportRow>(Lift(doc.RootElement), Ctx());
        Assert.NotNull(result);
        Assert.Contains("blob.core.windows.net", result.ToString());
    }
}

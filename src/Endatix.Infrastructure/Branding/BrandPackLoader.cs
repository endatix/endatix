using System.Text.Json;
using Endatix.Core.Branding;
using Endatix.Framework.Modules;
using Json.Schema;
using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Branding;

/// <summary>
/// Resolves the <see cref="DeploymentBrand"/> at startup: the built-in brand unless the white-labeling flag is on
/// and <c>ENDATIX_BRAND_DIR</c> points at a pack. A pack is validated in full against the embedded schema, and an
/// invalid one throws, naming each failing field path.
/// </summary>
internal static class BrandPackLoader
{
    /// <summary>The configuration key (an environment variable in a pod) holding the pack folder.</summary>
    internal const string BrandDirectoryKey = "ENDATIX_BRAND_DIR";

    private const string PackFileName = "brand.json";

    // These keywords only report that a nested schema failed; the nested failure names the field itself.
    private static readonly HashSet<string> SummaryKeywords = new(StringComparer.Ordinal) { "properties", "$ref", "allOf" };

    private static readonly EvaluationOptions SchemaEvaluation = new()
    {
        OutputFormat = OutputFormat.List,
        // Draft 2020-12 treats "format" as an annotation; the Hub's zod parse rejects a bad email or URL, so must we.
        RequireFormatValidation = true,
    };

    internal static DeploymentBrand Load(IConfiguration configuration)
    {
        var directory = configuration[BrandDirectoryKey];
        if (!EndatixModuleRegistration.IsFeatureFlagEnabled(configuration, Framework.FeatureFlags.FeatureFlags.WhiteLabeling)
            || string.IsNullOrWhiteSpace(directory))
        {
            return DeploymentBrand.BuiltIn;
        }

        var path = Path.Combine(directory, PackFileName);
        using var document = Parse(path);
        Validate(path, document.RootElement);
        return Map(document.RootElement);
    }

    private static JsonDocument Parse(string path)
    {
        if (!File.Exists(path))
        {
            throw new BrandPackException($"Brand pack file '{path}' was not found. Set {BrandDirectoryKey} to a folder that holds {PackFileName}.");
        }

        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new BrandPackException($"Brand pack file '{path}' is not valid JSON (line {ex.LineNumber + 1}).", ex);
        }
    }

    private static void Validate(string path, JsonElement pack)
    {
        var results = BrandPackSchema.Instance.Evaluate(pack, SchemaEvaluation);
        if (results.IsValid)
        {
            return;
        }

        var fieldFailures = (results.Details ?? [])
            .Where(detail => detail.Errors is { Count: > 0 })
            .SelectMany(detail => detail.Errors!
                .Where(error => !SummaryKeywords.Contains(error.Key))
                .Select(error => $"{FieldPath(detail)}: {error.Value}"))
            .Distinct(StringComparer.Ordinal);

        throw new BrandPackException($"Brand pack file '{path}' does not match the brand pack schema: {string.Join("; ", fieldFailures)}");
    }

    private static string FieldPath(EvaluationResults detail)
    {
        var location = detail.InstanceLocation.ToString();
        return location.Length == 0 ? "/" : location;
    }

    private static DeploymentBrand Map(JsonElement pack)
    {
        var builtIn = DeploymentBrand.BuiltIn;
        var name = pack.GetProperty("name").GetString()!;
        var email = pack.TryGetProperty("email", out var emailElement) ? emailElement : default;

        return new DeploymentBrand(
            ProductName: name,
            AppName: name,
            AssistantName: StringAt(pack, "personas", "assistant") ?? builtIn.AssistantName,
            WebsiteUrl: StringAt(pack, "links", "website") ?? builtIn.WebsiteUrl,
            Email: new BrandEmail(
                SenderName: StringOf(email, "senderName") ?? name,
                FromAddress: StringOf(email, "fromAddress"),
                SupportAddress: StringOf(email, "supportAddress") ?? builtIn.Email.SupportAddress));
    }

    private static string? StringAt(JsonElement pack, string group, string field) =>
        pack.TryGetProperty(group, out var element) ? StringOf(element, field) : null;

    private static string? StringOf(JsonElement element, string field) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(field, out var value) ? value.GetString() : null;
}

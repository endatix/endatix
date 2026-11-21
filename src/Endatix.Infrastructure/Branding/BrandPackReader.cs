using System.Net.Mail;
using System.Text.Json;
using Endatix.Core.Branding;

namespace Endatix.Infrastructure.Branding;

/// <summary>
/// Reads and checks the fields of <c>brand.json</c> the API uses: <c>version</c>, <c>name</c>, <c>links.website</c>,
/// <c>links.support</c> and <c>personas.assistant</c>. The rules are the brand pack schema's for those fields, plus no
/// blank values, no control characters in the name, which can reach a mail header as the sender name, and one bare
/// address in a <c>mailto:</c> support link. Assets, colours and the other links belong to the Hub, which validates
/// the whole pack.
/// </summary>
internal sealed class BrandPackReader
{
    private const int NameMaxLength = 60;
    private static readonly string[] LinkSchemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto];

    private const string MailtoPrefix = "mailto:";

    private readonly List<string> _failures = [];
    private readonly ICollection<string> _warnings;

    private BrandPackReader(ICollection<string> warnings)
    {
        _warnings = warnings;
    }

    /// <summary>
    /// Maps the pack, or throws <see cref="BrandPackException"/> naming every failing field. A field the API cannot
    /// use but the pack format allows keeps its built-in value and adds a line to <paramref name="warnings"/>.
    /// </summary>
    internal static DeploymentBrand Read(string path, JsonElement pack, ICollection<string> warnings)
    {
        var reader = new BrandPackReader(warnings);
        var brand = pack.ValueKind == JsonValueKind.Object ? reader.Map(pack) : reader.NotAnObject();
        if (reader._failures.Count > 0)
        {
            throw new BrandPackException($"Brand pack file '{path}' is not valid: {string.Join("; ", reader._failures)}");
        }

        return brand;
    }

    private DeploymentBrand NotAnObject()
    {
        _failures.Add("/: must be a JSON object");
        return DeploymentBrand.BuiltIn;
    }

    private DeploymentBrand Map(JsonElement pack)
    {
        CheckVersion(Field(pack, "version"));
        var builtIn = DeploymentBrand.BuiltIn;
        var name = Name("/name", Required("/name", Field(pack, "name")), NameMaxLength) ?? builtIn.ProductName;
        var links = Group(pack, "links");

        return new DeploymentBrand(
            ProductName: name,
            AppName: name,
            AssistantName: Text("/personas/assistant", Field(Group(pack, "personas"), "assistant")) ?? builtIn.AssistantName,
            WebsiteUrl: Link("/links/website", Field(links, "website")) ?? builtIn.WebsiteUrl,
            SupportAddress: SupportAddress(Field(links, "support")) ?? builtIn.SupportAddress);
    }

    private JsonElement? Required(string path, JsonElement? element)
    {
        if (element is null)
        {
            _failures.Add($"{path}: is required");
        }

        return element;
    }

    private void CheckVersion(JsonElement? version)
    {
        if (version is not { ValueKind: JsonValueKind.Number } number || !number.TryGetInt32(out var value) || value != 1)
        {
            _failures.Add("/version: must be 1");
        }
    }

    private JsonElement? Group(JsonElement pack, string group)
    {
        var element = Field(pack, group);
        if (element is null || element.Value.ValueKind == JsonValueKind.Object)
        {
            return element;
        }

        _failures.Add($"/{group}: must be a JSON object");
        return null;
    }

    private string? Name(string path, JsonElement? element, int maxLength)
    {
        var value = Text(path, element);
        if (value is not null && value.Any(char.IsControl))
        {
            return Fail(path, "must not contain control characters");
        }

        return value?.Length > maxLength
            ? Fail(path, $"must be at most {maxLength} characters")
            : value;
    }

    private string? Text(string path, JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        if (element.Value.ValueKind != JsonValueKind.String)
        {
            return Fail(path, "must be a string");
        }

        var value = element.Value.GetString()!;
        return string.IsNullOrWhiteSpace(value) ? Fail(path, "must not be blank") : value;
    }

    private string? Link(string path, JsonElement? element)
    {
        var value = Text(path, element);
        return value is null ? null : CheckLink(path, value);
    }

    private string? CheckLink(string path, string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && LinkSchemes.Contains(uri.Scheme)
            ? value
            : Fail(path, "must be an http, https or mailto URL");

    // Only a mailto: link names a support address; a web support page leaves the built-in one.
    private string? SupportAddress(JsonElement? element)
    {
        const string path = "/links/support";
        var link = Text(path, element);
        if (link is null)
        {
            return null;
        }

        if (!link.StartsWith(MailtoPrefix, StringComparison.OrdinalIgnoreCase))
        {
            CheckLink(path, link);
            return null;
        }

        return MailtoAddress(path, link);
    }

    private string? MailtoAddress(string path, string link)
    {
        // RFC 6068: the address ends at '?' (headers) or '#', and may be percent-encoded.
        var address = Uri.UnescapeDataString(link[MailtoPrefix.Length..].Split('?', '#')[0]);
        if (address.Contains(','))
        {
            _warnings.Add($"{path} names more than one address, so emails use the built-in support address");
            return null;
        }

        // A bare address only: "Name <a@b>" would reach the email copy as more than the address.
        return MailAddress.TryCreate(address, out var parsed) && parsed.Address == address
            ? address
            : Fail(path, "must be a mailto link to one email address");
    }

    private string? Fail(string path, string message)
    {
        _failures.Add($"{path}: {message}");
        return null;
    }

    private static JsonElement? Field(JsonElement? element, string field) =>
        element is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(field, out var found) ? found : null;
}

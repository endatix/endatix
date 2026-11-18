using System.Net.Mail;
using System.Text.Json;
using Endatix.Core.Branding;

namespace Endatix.Infrastructure.Branding;

/// <summary>
/// Reads and checks the fields of <c>brand.json</c> the API uses: <c>version</c>, <c>name</c>, <c>links.website</c>,
/// <c>personas.assistant</c> and <c>email</c>. The rules are the brand pack schema's for those fields, plus no blank
/// values and no control characters in names, which reach mail headers. Assets, colours and the other links belong
/// to the Hub, which validates the whole pack.
/// </summary>
internal sealed class BrandPackReader
{
    private const int NameMaxLength = 60;
    private static readonly string[] WebsiteSchemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto];

    private readonly List<string> _failures = [];

    private BrandPackReader()
    {
    }

    /// <summary>Maps the pack, or throws <see cref="BrandPackException"/> naming every failing field.</summary>
    internal static DeploymentBrand Read(string path, JsonElement pack)
    {
        var reader = new BrandPackReader();
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

        return new DeploymentBrand(
            ProductName: name,
            AppName: name,
            AssistantName: Text("/personas/assistant", Field(Group(pack, "personas"), "assistant")) ?? builtIn.AssistantName,
            WebsiteUrl: Website(Field(Group(pack, "links"), "website")) ?? builtIn.WebsiteUrl,
            Email: MapEmail(Group(pack, "email"), name));
    }

    private BrandEmail MapEmail(JsonElement? email, string productName) => new(
        SenderName: Name("/email/senderName", Field(email, "senderName")) ?? productName,
        FromAddress: EmailAddress("/email/fromAddress", Field(email, "fromAddress")),
        SupportAddress: EmailAddress("/email/supportAddress", Field(email, "supportAddress"))
            ?? DeploymentBrand.BuiltIn.Email.SupportAddress);

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

    private string? Name(string path, JsonElement? element, int? maxLength = null)
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

    private string? Website(JsonElement? element)
    {
        var value = Text("/links/website", element);
        if (value is null)
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && WebsiteSchemes.Contains(uri.Scheme)
            ? value
            : Fail("/links/website", "must be an http, https or mailto URL");
    }

    private string? EmailAddress(string path, JsonElement? element)
    {
        var value = Text(path, element);
        if (value is null)
        {
            return null;
        }

        // A bare address only: "Name <a@b>" would smuggle a display name past the sender name.
        return MailAddress.TryCreate(value, out var address) && address.Address == value
            ? value
            : Fail(path, "must be an email address");
    }

    private string? Fail(string path, string message)
    {
        _failures.Add($"{path}: {message}");
        return null;
    }

    private static JsonElement? Field(JsonElement? element, string field) =>
        element is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(field, out var found) ? found : null;
}

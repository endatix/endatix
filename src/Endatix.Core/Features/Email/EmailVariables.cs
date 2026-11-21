using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Branding;

namespace Endatix.Core.Features.Email;

/// <summary>
/// The variables every email can use, whichever path sends it: a stored template renders them, and a provider
/// template (<see cref="EmailWithTemplate.IsExternal"/>) receives them as its dynamic data. The deployment brand
/// contributes them; the email's own metadata wins on a clash.
/// </summary>
/// <param name="ProductName">The product name (<c>{{productName}}</c>), also used in composed copy.</param>
/// <param name="AppName">The name of the app people sign in to, used in composed copy.</param>
/// <param name="SupportAddress">The address people are told to contact (<c>{{supportAddress}}</c>).</param>
/// <param name="WebsiteUrl">The product website (<c>{{websiteUrl}}</c>).</param>
[Experimental("ENDATIX0004")]
public sealed record EmailVariables(string ProductName, string AppName, string SupportAddress, string WebsiteUrl)
{
    /// <summary>The variables the deployment brand contributes.</summary>
    public static EmailVariables From(DeploymentBrand brand) =>
        new(brand.ProductName, brand.AppName, brand.SupportAddress, brand.WebsiteUrl);

    /// <summary>The variable names and values, without any email's metadata.</summary>
    public IReadOnlyDictionary<string, string> Values => new Dictionary<string, string>
    {
        ["productName"] = ProductName,
        ["supportAddress"] = SupportAddress,
        ["websiteUrl"] = WebsiteUrl,
    };

    /// <summary>The variables merged with the email's metadata as text, for a stored template.</summary>
    public Dictionary<string, string> ForTemplate(IReadOnlyDictionary<string, object>? metadata)
    {
        var variables = new Dictionary<string, string>(Values);
        foreach (var (key, value) in metadata ?? new Dictionary<string, object>())
        {
            variables[key] = value?.ToString() ?? string.Empty;
        }

        return variables;
    }

    /// <summary>
    /// The variables merged with the email's metadata, for a provider template. Metadata values keep their type,
    /// so a provider template that reads a number, a flag or a nested object still gets one.
    /// </summary>
    public Dictionary<string, object> ForProvider(IReadOnlyDictionary<string, object>? metadata)
    {
        var data = new Dictionary<string, object>(metadata ?? new Dictionary<string, object>());
        foreach (var (key, value) in Values)
        {
            data.TryAdd(key, value);
        }

        return data;
    }
}

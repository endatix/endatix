using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Common;

/// <summary>
/// Group for public API endpoints.
/// </summary>
public sealed class PublicApiGroup : Group
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PublicApiGroup"/> class.
    /// Commont configuration for all public API endpoints.
    /// </summary>
    public PublicApiGroup() => Configure("public/", group => group.Description(ep =>
    {
        ep.WithTags("Public");
    }));
}
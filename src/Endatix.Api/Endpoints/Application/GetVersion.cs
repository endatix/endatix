using System.Reflection;
using Endatix.Core.Abstractions.Authorization;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Application;

/// <summary>
/// Returns the API release version to a signed-in Hub user.
/// The version is the assembly informational version with any SourceLink
/// commit suffix removed, so it matches the GitHub release tag.
/// </summary>
public sealed class GetVersion : EndpointWithoutRequest<Ok<ProductVersionResponse>>
{
    public override void Configure()
    {
        Get("system/version");
        Permissions(Actions.Access.Hub);
        Summary(s =>
        {
            s.Summary = "Get API version";
            s.Description = "Returns the Endatix API release version for a signed-in Hub user.";
            s.Responses[200] = "Version retrieved successfully.";
            s.Responses[401] = "Authentication required.";
            s.Responses[403] = "Hub access is required.";
        });
        Description(builder => builder
            .Produces<ProductVersionResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden));
    }

    public override Task<Ok<ProductVersionResponse>> ExecuteAsync(CancellationToken ct)
    {
        var assembly = typeof(GetVersion).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var version = ReleaseVersion.WithoutCommit(informational, assembly.GetName().Version);

        return Task.FromResult(TypedResults.Ok(new ProductVersionResponse(version)));
    }
}

/// <summary>API release version, without a SourceLink commit suffix.</summary>
public sealed record ProductVersionResponse(string Version);

public static class ReleaseVersion
{
    public static string WithoutCommit(string? informationalVersion, System.Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? informationalVersion[..plus] : informationalVersion;
        }

        return assemblyVersion?.ToString(3) ?? "unknown";
    }
}

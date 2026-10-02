using System.Reflection;
using Endatix.Core.Abstractions.Authorization;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Application;

/// <summary>
/// What this API build is, for a signed-in Hub user: the release version when the build is a release,
/// otherwise the branch and commit it was built from.
/// </summary>
public sealed class GetVersion : EndpointWithoutRequest<Ok<ProductVersionResponse>>
{
    private static readonly ProductVersionResponse Current = Read();
    public override void Configure()
    {
        Get("system/version");
        Permissions(Actions.Access.Hub);
        Summary(s =>
        {
            s.Summary = "Get API version";
            s.Description = "Returns the Endatix API release version, or the branch and commit of a build that is not a release, for a signed-in Hub user.";
            s.Responses[200] = "Version retrieved successfully.";
            s.Responses[401] = "Authentication required.";
            s.Responses[403] = "Hub access is required.";
        });
        Description(builder => builder
            .Produces<ProductVersionResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden));
    }

    public override Task<Ok<ProductVersionResponse>> ExecuteAsync(CancellationToken ct) =>
        Task.FromResult(TypedResults.Ok(Current));

    private static ProductVersionResponse Read()
    {
        var assembly = typeof(GetVersion).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var branch = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == BuildIdentity.BranchMetadataKey)
            ?.Value;

        return BuildIdentity.From(informational, branch);
    }
}

/// <param name="Version">Release version (matches the GitHub tag without <c>v</c>); null for a build that is not a release.</param>
/// <param name="Branch">Branch the build came from; set only for builds outside the release pipeline.</param>
/// <param name="Commit">Full commit SHA, when the build recorded one.</param>
public sealed record ProductVersionResponse(string? Version, string? Branch, string? Commit);

internal static class BuildIdentity
{
    /// <summary>Written by the <c>ResolveLocalVersion</c> target in <c>Directory.Build.props</c>.</summary>
    public const string BranchMetadataKey = "GitBranch";

    /// <summary>The placeholder in <c>Directory.Build.props</c>; never a release.</summary>
    private const string LocalVersionPrefix = "0.0.0";

    /// <summary>
    /// Splits an InformationalVersion such as <c>0.8.0+abc123</c> (SourceLink appends the commit) into the
    /// release version and the commit. The placeholder, CI validation builds (<c>0.0.0-*</c>) and a blank value
    /// are not releases.
    /// </summary>
    public static ProductVersionResponse From(string? informationalVersion, string? branch)
    {
        var (version, commit) = Split(informationalVersion);
        var isRelease = version is not null && !version.StartsWith(LocalVersionPrefix, StringComparison.Ordinal);

        return new ProductVersionResponse(
            isRelease ? version : null,
            NullIfBlank(branch),
            commit);
    }

    private static (string? Version, string? Commit) Split(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return (null, null);
        }

        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0
            ? (NullIfBlank(informationalVersion[..plus]), NullIfBlank(informationalVersion[(plus + 1)..]))
            : (informationalVersion.Trim(), null);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

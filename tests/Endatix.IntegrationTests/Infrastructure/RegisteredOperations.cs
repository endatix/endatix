using Endatix.Core.Infrastructure.Attributes;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticAssets;

namespace Endatix.IntegrationTests;

internal static class RegisteredOperations
{
    private static readonly string[] _allModules = ["core", "jobs", "audience", "reporting", "dashboard"];

    internal static string[] ModulesIn(string profile) => profile switch
    {
        "core" => ["core"],
        "jobs" => ["core", "jobs"],
        "audience" => ["core", "jobs", "audience"],
        "reporting" => ["core", "jobs", "reporting"],
        "dashboard-read" or "dashboard-write" => _allModules,
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };

    internal static IEnumerable<string> Reviewed(string[] modules)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Snapshots", "required-action-inventory.txt");
        return File.ReadLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(ParseRow)
            .Where(row => modules.Contains(row.Module))
            .Select(row => $"{row.Access} {row.Operation}");
    }

    private sealed record InventoryRow(string Module, string Access, string Operation);

    private static InventoryRow ParseRow(string line)
    {
        var fields = line.Split(' ', 4);
        Assert.True(fields.Length == 4 && _reviewedAccess.TryGetValue(fields[1], out var access) && access.Contains(fields[2])
            && _allModules.Contains(fields[0]), $"Invalid row in Snapshots/required-action-inventory.txt: {line}");
        return new(fields[0], fields[2], fields[3]);
    }

    private static readonly Dictionary<string, string[]> _reviewedAccess = new()
    {
        ["anonymous"] = ["anonymous"],
        ["open"] = ["resolver", "authenticated-resolver", "platform-resolver", "open-no-policy"],
        ["respondent"] = ["anonymous", "form-token"],
        ["gated"] = ["account", "platform-admin", "no-policy", "mixed-policy"],
    };

    internal static IEnumerable<string> Differences(IEnumerable<string> actual, IEnumerable<string> reviewed)
    {
        var expected = reviewed.ToArray();
        var registered = actual.ToArray();
        registered.Should().OnlyHaveUniqueItems();
        expected.Should().OnlyHaveUniqueItems();
        return registered.Except(expected).Select(operation => $"Unclassified: {operation}")
            .Concat(expected.Except(registered).Select(operation => $"Missing: {operation}"));
    }

    // Fingerprinted assets have multiple negotiated representations and vary by SDK and build configuration.
    // Only the framework's actual static-file descriptors are assets; an executable GET under the same path is not.
    internal static IEnumerable<string> Operations(IEnumerable<RouteEndpoint> registrations) => registrations
        .Where(route => route.Metadata.GetMetadata<StaticAssetDescriptor>() is null)
        .SelectMany(route => Methods(route)
            .Select(method => $"{Access(route.Metadata)} {method} {route.RoutePattern.RawText}"));

    internal static IReadOnlyList<string> Methods(RouteEndpoint route) =>
        route.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods is { Count: > 0 } methods ? methods : ["*"];

    internal static string Access(EndpointMetadataCollection metadata)
    {
        var policies = metadata.GetOrderedMetadata<IAuthorizeData>();
        var resolver = metadata.GetMetadata<AllowWithRequiredActionsAttribute>() is not null
            || metadata.GetMetadata<EndpointDefinition>()?.EndpointAttributes?.OfType<AllowWithRequiredActionsAttribute>().Any() == true;
        if (resolver)
        {
            return ResolverAccess(metadata, policies);
        }
        if (metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return "anonymous";
        }
        if (policies.Any(p => p.Policy == AuthorizationPolicies.PublicResourceAccess))
        {
            return RespondentAccess(policies, metadata);
        }
        if (policies.Any(p => p.Policy == AuthorizationPolicies.PlatformAdminAccess))
        {
            return "platform-admin";
        }
        if (policies.Count == 0)
        {
            return "no-policy";
        }
        return HasAccountGrant(metadata, policies) ? "account" : "authenticated";
    }

    private static string ResolverAccess(EndpointMetadataCollection metadata, IReadOnlyList<IAuthorizeData> policies)
    {
        if (metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return "anonymous-resolver";
        }
        if (policies.Count == 0)
        {
            return "open-no-policy";
        }
        if (policies.Any(p => p.Policy == AuthorizationPolicies.PlatformAdminAccess))
        {
            return "platform-resolver";
        }
        return HasAccountGrant(metadata, policies) ? "resolver" : "authenticated-resolver";
    }

    private static bool HasAccountGrant(EndpointMetadataCollection metadata, IReadOnlyList<IAuthorizeData> policies)
    {
        var definition = metadata.GetMetadata<EndpointDefinition>();
        return definition is null || definition.AllowedPermissions is { Count: > 0 } ||
            definition.AllowedRoles is { Count: > 0 } || definition.AllowedClaimTypes is { Count: > 0 } ||
            definition.AllowedScopes is { Count: > 0 } ||
            policies.Any(p => !string.IsNullOrEmpty(p.Policy) && p.Policy != definition.SecurityPolicyName);
    }

    private static string RespondentAccess(IReadOnlyList<IAuthorizeData> policies, EndpointMetadataCollection metadata)
    {
        if (metadata.GetMetadata<AuthorizationPolicy>() is not null || metadata.GetMetadata<IAuthorizationRequirementData>() is not null)
        {
            return "mixed-policy";
        }
        var definition = metadata.GetMetadata<EndpointDefinition>();
        // FastEndpoints adds the endpoint's own generated policy alongside its declared public-resource policy.
        var generatedPolicy = definition is
        {
            AllowedPermissions: not { Count: > 0 }, AllowedRoles: not { Count: > 0 },
            AllowedClaimTypes: not { Count: > 0 }, AllowedScopes: not { Count: > 0 }
        } ? definition.SecurityPolicyName : null;
        var respondentOnly = policies.All(p =>
            (p.Policy == AuthorizationPolicies.PublicResourceAccess || (generatedPolicy is not null && p.Policy == generatedPolicy))
            && string.IsNullOrEmpty(p.Roles) && string.IsNullOrEmpty(p.AuthenticationSchemes));
        return respondentOnly ? "form-token" : "mixed-policy";
    }
}

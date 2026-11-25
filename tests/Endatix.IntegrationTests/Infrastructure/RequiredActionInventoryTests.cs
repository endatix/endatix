using Endatix.Core.Infrastructure.Attributes;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed partial class RequiredActionInventoryTests(EndatixIntegrationWebHostFixture fixture)
{
    [Theory]
    [InlineData("core")]
    [InlineData("jobs")]
    [InlineData("audience")]
    [InlineData("reporting")]
    [InlineData("dashboard-read")]
    [InlineData("dashboard-write")]
    public async Task Registered_operations_match_the_reviewed_inventory(string profile)
    {
        // Arrange
        var modules = ModulesIn(profile);
        await using var factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        await using var host = factory.WithWebHostBuilder(builder => ConfigureProfile(builder, profile));

        // Act
        var registrations = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.Cast<RouteEndpoint>();
        var differences = Differences(Operations(registrations), Reviewed(modules));

        // Assert
        differences.Should().BeEmpty();
        if (modules.Contains("dashboard"))
        {
            var readOnly = profile == "dashboard-read";
            host.Services.GetRequiredService<IOptions<QuartzHttpApiOptions>>().Value.ReadOnly.Should().Be(readOnly);
            host.Services.GetRequiredService<IOptions<QuartzDashboardOptions>>().Value.ReadOnly.Should().Be(readOnly);
        }
    }

    [Fact]
    public void A_new_anonymous_registration_requires_its_own_review()
    {
        // Arrange
        var route = ExampleRoute(new HttpMethodMetadata(["GET"]), new AllowAnonymousAttribute());
        var reviewed = Reviewed(ModulesIn("core")).ToArray();

        // Act
        var differences = Differences(reviewed.Concat(Operations([route])), reviewed);

        // Assert
        differences.Should().Equal("Unclassified: anonymous GET /inventory-example");
    }

    [Fact]
    public void An_empty_method_list_requires_review_as_a_wildcard_registration()
    {
        // Arrange
        var route = ExampleRoute(new HttpMethodMetadata([]), new AllowAnonymousAttribute());

        // Act
        var differences = Differences(Operations([route]), []);

        // Assert
        differences.Should().Equal("Unclassified: anonymous * /inventory-example");
    }

    [Theory]
    [InlineData("PublicResourceAccess", "PlatformAdmin")]
    [InlineData("PlatformAdmin", "PublicResourceAccess")]
    public void A_mixed_policy_registration_no_longer_matches_the_reviewed_respondent(string first, string second)
    {
        // Arrange
        var route = ExampleRoute(new HttpMethodMetadata(["GET"]), new AuthorizeAttribute(first), new AuthorizeAttribute(second));

        // Act
        var differences = Differences(Operations([route]), ["form-token GET /inventory-example"]);

        // Assert
        differences.Should().Equal("Unclassified: mixed-policy GET /inventory-example", "Missing: form-token GET /inventory-example");
    }

    private static RouteEndpoint ExampleRoute(params object[] metadata) =>
        new(_ => Task.CompletedTask, RoutePatternFactory.Parse("/inventory-example"), 0, new EndpointMetadataCollection(metadata), null);

    [Fact]
    public void A_direct_authorization_policy_is_not_a_respondent_only_registration()
    {
        var route = ExampleRoute(new HttpMethodMetadata(["GET"]), new AuthorizeAttribute(AuthorizationPolicies.PublicResourceAccess),
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        Assert.Equal(["mixed-policy GET /inventory-example"], Operations([route]));
    }

    private static string[] ModulesIn(string profile) => profile switch
    {
        "core" => ["core"],
        "jobs" => ["core", "jobs"],
        "audience" => ["core", "jobs", "audience"],
        "reporting" => ["core", "jobs", "reporting"],
        "dashboard-read" or "dashboard-write" => ["core", "jobs", "audience", "reporting", "dashboard"],
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };

    private static void ConfigureProfile(IWebHostBuilder builder, string profile)
    {
        var modules = ModulesIn(profile);
        builder.UseSetting("Endatix:FeatureFlags:JobsModule", modules.Contains("jobs").ToString());
        builder.UseSetting("Endatix:FeatureFlags:PersonalizationModule", modules.Contains("audience").ToString());
        builder.UseSetting("Endatix:FeatureFlags:ReportingModule", modules.Contains("reporting").ToString());
        builder.UseSetting("Endatix:FeatureFlags:MultiTenancy", modules.Contains("dashboard").ToString());
        builder.UseSetting("Endatix:BackgroundJobs:Dashboard:Enabled", modules.Contains("dashboard").ToString());
        builder.UseSetting("Endatix:BackgroundJobs:Dashboard:AllowWrites", (profile == "dashboard-write").ToString());
    }

    private static IEnumerable<string> Reviewed(string[] modules)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Snapshots", "required-action-inventory.txt");
        return File.ReadLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(CheckedRow)
            .Where(fields => modules.Contains(fields[0]))
            .Select(fields => $"{fields[2]} {fields[3]}");
    }

    private static string[] CheckedRow(string line)
    {
        var fields = line.Split(' ', 4);
        Assert.True(_reviewedAccess.TryGetValue(fields[1], out var access) && access.Contains(fields[2])
            && ModulesIn("dashboard-write").Contains(fields[0]), $"Invalid inventory row: {line}");
        return fields;
    }

    private static readonly Dictionary<string, string[]> _reviewedAccess = new()
    {
        ["anonymous"] = ["anonymous"],
        ["open"] = ["resolver", "open-no-policy"],
        ["respondent"] = ["anonymous", "form-token"],
        ["gated"] = ["account", "platform-admin", "no-policy", "mixed-policy"],
    };

    private static IEnumerable<string> Differences(IEnumerable<string> actual, IEnumerable<string> reviewed)
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
    private static IEnumerable<string> Operations(IEnumerable<RouteEndpoint> registrations) => registrations
        .Where(route => route.Metadata.GetMetadata<StaticAssetDescriptor>() is null)
        .SelectMany(route => Methods(route)
            .Select(method => $"{Access(route.Metadata)} {method} {route.RoutePattern.RawText}"));

    private static IReadOnlyList<string> Methods(RouteEndpoint route) =>
        route.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods is { Count: > 0 } methods ? methods : ["*"];

    private static string Access(EndpointMetadataCollection metadata)
    {
        var policies = metadata.GetOrderedMetadata<IAuthorizeData>();
        var resolver = metadata.GetMetadata<AllowWithRequiredActionsAttribute>() is not null
            || metadata.GetMetadata<EndpointDefinition>()?.EndpointType.IsDefined(typeof(AllowWithRequiredActionsAttribute), false) == true;
        if (resolver)
        {
            return metadata.GetMetadata<IAllowAnonymous>() is not null ? "anonymous-resolver"
                : policies.Count > 0 ? "resolver" : "open-no-policy";
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
        return policies.Count > 0 ? "account" : "no-policy";
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

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
public sealed class RequiredActionInventoryTests(EndatixIntegrationWebHostFixture fixture)
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
        await using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Endatix:FeatureFlags:JobsModule", modules.Contains("jobs").ToString());
            builder.UseSetting("Endatix:FeatureFlags:PersonalizationModule", modules.Contains("audience").ToString());
            builder.UseSetting("Endatix:FeatureFlags:ReportingModule", modules.Contains("reporting").ToString());
            builder.UseSetting("Endatix:FeatureFlags:MultiTenancy", modules.Contains("dashboard").ToString());
            builder.UseSetting("Endatix:BackgroundJobs:Dashboard:Enabled", modules.Contains("dashboard").ToString());
            builder.UseSetting("Endatix:BackgroundJobs:Dashboard:AllowWrites", (profile == "dashboard-write").ToString());
        });

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
        var route = new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/inventory-example"), 0,
            new EndpointMetadataCollection(new HttpMethodMetadata(["GET"]), new AllowAnonymousAttribute()), null);
        var reviewed = Reviewed(ModulesIn("core")).ToArray();

        // Act
        var differences = Differences(reviewed.Concat(Operations([route])), reviewed);

        // Assert
        differences.Should().Equal("Unclassified: anonymous GET /inventory-example");
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
        var placement = fields[1].Split('#');
        Assert.True(_reviewedAccess.TryGetValue(placement[0], out var access) && access.Contains(fields[2])
            && HasDeliveryOwner(placement) && ModulesIn("dashboard-write").Contains(fields[0]), $"Invalid inventory row: {line}");
        return fields;
    }

    private static readonly Dictionary<string, string[]> _reviewedAccess = new()
    {
        ["anonymous"] = ["anonymous"],
        ["resolver"] = ["resolver"],
        ["respondent"] = ["anonymous", "form-token"],
        ["asset-handler"] = ["no-policy", "platform-admin"],
        ["package-pending"] = ["platform-admin"],
        ["policy-pending"] = ["account", "platform-admin"],
        ["operation-pending"] = ["account", "platform-admin"],
        ["operation-dormant"] = ["account", "platform-admin"],
    };

    private static bool HasDeliveryOwner(string[] placement) =>
        placement[0].EndsWith("-pending", StringComparison.Ordinal)
            ? placement is [_, "1825" or "1826" or "1827" or "1828" or "1829"]
            : placement.Length == 1;

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
        .SelectMany(route => (route.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"])
            .Select(method => $"{Access(route.Metadata)} {method} {route.RoutePattern.RawText}"));

    private static string Access(EndpointMetadataCollection metadata)
    {
        var policies = metadata.GetOrderedMetadata<IAuthorizeData>();
        var resolver = metadata.GetMetadata<AllowWithRequiredActionsAttribute>() is not null
            || metadata.GetMetadata<EndpointDefinition>()?.EndpointType.IsDefined(typeof(AllowWithRequiredActionsAttribute), false) == true;
        if (resolver)
        {
            return policies.Count > 0 && metadata.GetMetadata<IAllowAnonymous>() is null ? "resolver" : "unprotected-resolver";
        }
        if (metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return "anonymous";
        }
        if (policies.Any(p => p.Policy == AuthorizationPolicies.PublicResourceAccess))
        {
            return "form-token";
        }
        if (policies.Any(p => p.Policy == AuthorizationPolicies.PlatformAdminAccess))
        {
            return "platform-admin";
        }
        return policies.Count > 0 ? "account" : "no-policy";
    }
}

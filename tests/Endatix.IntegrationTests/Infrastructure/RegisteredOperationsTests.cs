using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using static Endatix.IntegrationTests.RegisteredOperations;

namespace Endatix.IntegrationTests;

public sealed class RegisteredOperationsTests
{
    [Theory]
    [InlineData(null, "authenticated")]
    [InlineData("BackOffice", "account")]
    public void Minimal_routes_distinguish_default_authentication_from_named_grants(string? policy, string expected)
    {
        var route = ExampleRoute(new AuthorizeAttribute { Policy = policy }, new HttpMethodMetadata(["GET"]));
        Assert.Equal([$"{expected} GET /inventory-example"], Operations([route]));
    }

    [Fact]
    public void A_default_authenticated_operation_does_not_match_a_permission_protected_registration()
    {
        // Arrange
        var definition = new EndpointDefinition(typeof(Api.Endpoints.DataLists.List), typeof(EmptyRequest), typeof(EmptyResponse));
        var route = ExampleRoute(definition, new AuthorizeAttribute(definition.SecurityPolicyName), new HttpMethodMetadata(["GET"]));

        // Act
        var differences = Differences(Operations([route]), ["account GET /inventory-example"]);

        // Assert
        differences.Should().Equal("Unclassified: authenticated GET /inventory-example", "Missing: account GET /inventory-example");
    }

    [Fact]
    public void A_new_anonymous_registration_requires_its_own_review()
    {
        // Arrange
        var route = ExampleRoute(new HttpMethodMetadata(["GET"]), new AllowAnonymousAttribute());
        string[] reviewed = [];

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
}

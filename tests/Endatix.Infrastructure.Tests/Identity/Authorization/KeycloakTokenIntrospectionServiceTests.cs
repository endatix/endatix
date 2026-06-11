using System.Net;
using System.Text;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed class KeycloakTokenIntrospectionServiceTests
{
    [Fact]
    public async Task IntrospectAsync_ReturnsRolesAndProfileFromSameResponse()
    {
        // Arrange
        const string responseContent = """
        {
          "active": true,
          "email": " operator@example.com ",
          "preferred_username": " operator-user ",
          "resource_access": {
            "endatix-hub": {
              "roles": ["admin", "creator"]
            }
          }
        }
        """;
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
        });
        var service = CreateService(handler);
        var options = new KeycloakOptions
        {
            Issuer = "https://keycloak.test",
            ClientId = "endatix-hub",
            ClientSecret = "client-secret",
            Authorization = new KeycloakOptions.KeycloakAuthorizationStrategyOptions()
        };

        // Act
        var result = await service.IntrospectAsync("access-token", options, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExternalRoles.Should().BeEquivalentTo(["admin", "creator"]);
        result.Value.Profile.Email.Should().Be("operator@example.com");
        result.Value.Profile.DisplayName.Should().Be("operator-user");
        handler.RequestCount.Should().Be(1);
    }

    private static KeycloakTokenIntrospectionService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient().Returns(httpClient);

        return new KeycloakTokenIntrospectionService(httpClientFactory);
    }

    private sealed class CapturingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(response);
        }
    }
}

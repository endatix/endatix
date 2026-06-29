using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Provisioning;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Identity.Provisioning;

public sealed class KeycloakUserInfoProfileServiceTests
{
    [Fact]
    public async Task GetProfileAsync_WithPreferredUsernameOnly_UsesItAsDisplayName()
    {
        // Arrange
        CapturingHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "email": " external@example.com ",
                  "preferred_username": " preferred-user "
                }
                """,
                Encoding.UTF8,
                "application/json")
        });
        KeycloakUserInfoProfileService service = CreateService(handler);

        // Act
        var result = await service.GetProfileAsync("access-token", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("external@example.com");
        result.Value.DisplayName.Should().Be("preferred-user");
        handler.RequestMethod.Should().Be(HttpMethod.Get);
        handler.RequestUri.Should().Be("https://keycloak.test/protocol/openid-connect/userinfo");
        handler.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "access-token").ToString());
    }

    [Fact]
    public async Task GetProfileAsync_WhenUserInfoRequestFails_ReturnsError()
    {
        // Arrange
        CapturingHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.BadGateway));
        KeycloakUserInfoProfileService service = CreateService(handler);

        // Act
        var result = await service.GetProfileAsync("access-token", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Failed to get Keycloak UserInfo profile.");
    }

    private static KeycloakUserInfoProfileService CreateService(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient().Returns(httpClient);

        KeycloakOptions options = new()
        {
            Issuer = "https://keycloak.test"
        };

        return new KeycloakUserInfoProfileService(httpClientFactory, Options.Create(options));
    }

    private sealed class CapturingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpMethod? RequestMethod { get; private set; }
        public string? RequestUri { get; private set; }
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestMethod = request.Method;
            RequestUri = request.RequestUri?.ToString();
            Authorization = request.Headers.Authorization?.ToString();

            return Task.FromResult(response);
        }
    }
}

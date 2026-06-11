using System.Net;
using System.Text;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed class KeycloakTokenIntrospectionServiceTests
{
    [Theory]
    [InlineData("""{"active":true}""", true)]
    [InlineData("""{"active":false}""", false)]
    [InlineData("""{"active":"true"}""", false)]
    [InlineData("""{"resource_access":{}}""", false)]
    public async Task IntrospectAsync_TreatsActivePropertyAsFailClosed(string responseBody, bool expectSuccess)
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient().Returns(new HttpClient(handler));
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));

        KeycloakTokenIntrospectionService service = new(httpClientFactory);
        KeycloakOptions options = new()
        {
            Issuer = "https://keycloak.example/realms/endatix",
            ClientId = "endatix-hub",
            ClientSecret = "secret",
            Audience = "endatix-hub"
        };

        Result<KeycloakTokenIntrospectionResult> result = await service.IntrospectAsync(
            "token-123",
            options,
            CancellationToken.None);

        result.IsSuccess.Should().Be(expectSuccess);
        if (!expectSuccess)
        {
            result.Errors.Should().Contain("Token is not active.");
        }
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}

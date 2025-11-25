using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Authorization.Strategies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed class KeycloakTokenIntrospectionAuthorizationTests
{
    private const string TestIssuer = "https://keycloak.test";

    [Fact]
    public void CanHandle_ReturnsFalse_WhenIssuerMissing()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = context.Strategy.CanHandle(principal);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_ReturnsTrue_WhenIssuerMatchesProvider()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        var principal = CreatePrincipal("42", TestIssuer);

        // Act
        var result = context.Strategy.CanHandle(principal);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsError_WhenProviderCannotHandle()
    {
        // Arrange
        var context = CreateTestContext();
        var principal = CreatePrincipal("42", "other");

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Provider cannot handle the given issuer");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsDefault_WhenAuthorizationSectionMissing()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.Options.Authorization = null;
        var principal = CreatePrincipal("123", TestIssuer);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Roles.Should().Contain(SystemRole.Authenticated.Name);
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsError_WhenAuthorizationHeaderMissing()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.ClearHeader();
        var principal = CreatePrincipal("123", TestIssuer);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Authorization header is not found");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsError_WhenIntrospectionFails()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.SetHttpResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var principal = CreatePrincipal("123", TestIssuer);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Failed to introspect token");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsError_WhenRolesExtractionFails()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.SetHttpResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"resource_access":{}}""", Encoding.UTF8, "application/json")
        });
        var principal = CreatePrincipal("123", TestIssuer);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Failed to get roles");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsError_WhenMappingFails()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.SetSuccessfulIntrospectionResponse(["kc-admin"]);
        context.Mapper.MapToAppRolesAsync(Arg.Any<string[]>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(IExternalAuthorizationMapper.MappingResult.Failure("mapping-error"));
        var principal = CreatePrincipal("123", TestIssuer);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("mapping-error");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsAuthorizationData_WhenMappingSucceeds()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.SetSuccessfulIntrospectionResponse(["kc-admin"]);
        context.Mapper.MapToAppRolesAsync(Arg.Any<string[]>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(IExternalAuthorizationMapper.MappingResult.Success(["Admin"], ["perm.read"]));
        var principal = CreatePrincipal("123", TestIssuer);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Roles.Should().Contain("Admin");
        result.Value.Permissions.Should().Contain("perm.read");
    }

    private static ClaimsPrincipal CreatePrincipal(string userId, string issuer)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimNames.UserId, userId),
            new Claim(JwtRegisteredClaimNames.Iss, issuer)
        ], "test");

        return new ClaimsPrincipal(identity);
    }

    private static TestContext CreateTestContext()
    {
        var registry = new AuthProviderRegistry();
        var optionsInstance = new KeycloakOptions
        {
            Issuer = TestIssuer,
            ClientId = "endatix-hub",
            ClientSecret = "client-secret",
            DefaultTenantId = 77,
            Authorization = new KeycloakOptions.KeycloakAuthorizationStrategyOptions
            {
                RoleMappings = new Dictionary<string, string>
                {
                    { "kc-admin", SystemRole.Admin.Name }
                }
            }
        };
        var options = Options.Create(optionsInstance);

        var mapper = Substitute.For<IExternalAuthorizationMapper>();
        mapper.MapToAppRolesAsync(Arg.Any<string[]>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(IExternalAuthorizationMapper.MappingResult.Success(["User"], ["perm.view"]));

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        httpContextAccessor.HttpContext!.Request.Headers["Authorization"] = "Bearer token-123";

        var handler = new ConfigurableHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient().Returns(httpClient);
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var logger = Substitute.For<ILogger<KeycloakTokenIntrospectionAuthorization>>();

        var strategy = new KeycloakTokenIntrospectionAuthorization(
            registry,
            options,
            mapper,
            httpContextAccessor,
            httpClientFactory,
            logger);

        return new TestContext(
            Registry: registry,
            Options: optionsInstance,
            Mapper: mapper,
            HttpContextAccessor: httpContextAccessor,
            HttpClientFactory: httpClientFactory,
            Handler: handler,
            Strategy: strategy);
    }

    private sealed record TestContext(
        AuthProviderRegistry Registry,
        KeycloakOptions Options,
        IExternalAuthorizationMapper Mapper,
        IHttpContextAccessor HttpContextAccessor,
        IHttpClientFactory HttpClientFactory,
        ConfigurableHttpMessageHandler Handler,
        KeycloakTokenIntrospectionAuthorization Strategy)
    {
        public void RegisterProvider(string issuer, bool activate)
        {
            var provider = new KeycloakAuthProvider();
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"Endatix:Auth:Providers:{provider.SchemeName}:Enabled"] = "true",
                    [$"Endatix:Auth:Providers:{provider.SchemeName}:Issuer"] = issuer,
                    [$"Endatix:Auth:Providers:{provider.SchemeName}:ClientId"] = Options.ClientId,
                    [$"Endatix:Auth:Providers:{provider.SchemeName}:ClientSecret"] = Options.ClientSecret
                })
                .Build();

            Registry.RegisterProvider<KeycloakOptions>(provider, services, configuration);
            SetProviderIssuer(provider, issuer);

            if (activate)
            {
                Registry.AddActiveProvider(provider);
            }
        }

        public void ClearHeader()
        {
            HttpContextAccessor.HttpContext!.Request.Headers.Remove("Authorization");
        }

        public void SetHttpResponse(HttpResponseMessage response)
        {
            Handler.SetResponse(response);
        }

        public void SetSuccessfulIntrospectionResponse(IEnumerable<string> roles)
        {
            var rolesJson = string.Join(',', roles.Select(r => $"\"{r}\""));
            var payload = $$"""
            {
              "resource_access": {
                "{{Options.ClientId}}": {
                  "roles": [{{rolesJson}}]
                }
              }
            }
            """;

            SetHttpResponse(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ConfigurableHttpMessageHandler : HttpMessageHandler
    {
        private HttpResponseMessage _response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"resource_access":{}}""", Encoding.UTF8, "application/json")
        };

        public void SetResponse(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    private static void SetProviderIssuer(KeycloakAuthProvider provider, string issuer)
    {
        var field = typeof(KeycloakAuthProvider)
            .GetField("_cachedIssuer", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(provider, issuer);
    }
}


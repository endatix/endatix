using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.Infrastructure.Identity.Authorization.Strategies;
using Endatix.Infrastructure.Identity.Provisioning;
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
    public async Task GetAuthorizationDataAsync_ReturnsSuccessWithEmptyRoles_WhenAuthorizationSectionMissing()
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
        result.Value.Roles.Should().BeEquivalentTo(SystemRole.Authenticated.Name);
        result.Value.Permissions.Should().BeEquivalentTo(SystemRole.Authenticated.Permissions);
        result.Value.UserId.Should().Be("123");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsError_WhenAccessTokenMissing()
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
        result.Errors.Should().Contain("Access token is not found");
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
        result.Errors.Should().Contain("Failed to introspect token.");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsError_WhenRolesExtractionFails()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.SetHttpResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"active":true,"resource_access":{}}""", Encoding.UTF8, "application/json")
        });
        var principal = CreatePrincipal("123", TestIssuer);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Failed to get roles.");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsError_WhenMappingFails()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.SetSuccessfulIntrospectionResponse(["kc-admin"]);
        context.Mapper.Result = IExternalAuthorizationMapper.MappingResult.Failure("mapping-error");
        var principal = CreatePrincipal("123", TestIssuer);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("mapping-error");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsInvalid_WhenEmailClaimMissing()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.SetSuccessfulIntrospectionResponse(["kc-admin"]);
        context.ExternalOperatorProvisioner
            .ProvisionAsync(
                context.Options.DefaultTenantId,
                AuthProviders.Keycloak,
                "123",
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Is<ExternalIdentityProfile>(profile => profile.Email == null),
                Arg.Any<CancellationToken>())
            .Returns(Result<AppUser>.Invalid(new ValidationError("Operator email is required.")));
        var principal = CreatePrincipal("123", TestIssuer, includeEmail: false);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle(error => error.ErrorMessage == "Operator email is required.");
        await context.ExternalOperatorProvisioner
            .Received(1)
            .ProvisionAsync(
                context.Options.DefaultTenantId,
                AuthProviders.Keycloak,
                "123",
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Is<ExternalIdentityProfile>(profile => profile.Email == null),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_UsesIntrospectionProfile_WhenPrincipalEmailMissing()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.SetSuccessfulIntrospectionResponse(
            ["kc-admin"],
            email: "operator@example.com",
            preferredUsername: "operator-user");
        var principal = CreatePrincipal("123", TestIssuer, includeEmail: false);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        context.UserInfoProfileService.CallCount.Should().Be(0);
        await context.ExternalOperatorProvisioner
            .Received(1)
            .ProvisionAsync(
                context.Options.DefaultTenantId,
                AuthProviders.Keycloak,
                "123",
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Is<ExternalIdentityProfile>(profile =>
                    profile.Email == "operator@example.com" &&
                    profile.DisplayName == "operator-user"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsAuthorizationData_WhenMappingSucceeds()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.SetSuccessfulIntrospectionResponse(["kc-admin"]);
        context.Mapper.Result = IExternalAuthorizationMapper.MappingResult.Success(["Admin"], ["perm.read"]);
        var principal = CreatePrincipal("123", TestIssuer);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Roles.Should().Contain("Admin");
        result.Value.Permissions.Should().Contain("perm.read");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_UsesUserInfo_WhenPrincipalAndIntrospectionProfileAreMissingEmail()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.SetSuccessfulIntrospectionResponse(["kc-admin"]);
        context.UserInfoProfileService.ProfileResult =
            Result.Success(new ExternalIdentityProfile("userinfo@example.com", "userinfo-user"));
        var principal = CreatePrincipal("123", TestIssuer, includeEmail: false);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        context.UserInfoProfileService.CallCount.Should().Be(1);
        context.UserInfoProfileService.AccessToken.Should().Be("token-123");
        await context.ExternalOperatorProvisioner
            .Received(1)
            .ProvisionAsync(
                context.Options.DefaultTenantId,
                AuthProviders.Keycloak,
                "123",
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Is<ExternalIdentityProfile>(profile =>
                    profile.Email == "userinfo@example.com" &&
                    profile.DisplayName == "userinfo-user"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_SkipsUserInfo_WhenExistingUserHasDisplayName()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterProvider(TestIssuer, activate: true);
        context.SetSuccessfulIntrospectionResponse(["kc-admin"]);
        context.ExternalOperatorProfileReader.DisplayName = "existing-user";
        var principal = CreatePrincipal("123", TestIssuer);

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        context.UserInfoProfileService.CallCount.Should().Be(0);
    }

    private static ClaimsPrincipal CreatePrincipal(string userId, string issuer, bool includeEmail = true)
    {
        List<Claim> claims =
        [
            new Claim(ClaimNames.UserId, userId),
            new Claim(JwtRegisteredClaimNames.Iss, issuer)
        ];

        if (includeEmail)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, "operator@example.com"));
        }

        ClaimsIdentity identity = new(claims, "test");

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

        var mapper = new StubExternalAuthorizationMapper();

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        httpContextAccessor.HttpContext!.Request.Headers.Authorization = "Bearer token-123";

        var handler = new ConfigurableHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient().Returns(httpClient);
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        var tokenIntrospectionService = new KeycloakTokenIntrospectionService(httpClientFactory);
        var externalOperatorProfileReader = new StubExternalOperatorProfileReader();
        var userInfoProfileService = new StubKeycloakUserInfoProfileService();
        var profileResolver = new KeycloakExternalIdentityProfileResolver(
            externalOperatorProfileReader,
            userInfoProfileService,
            Substitute.For<ILogger<KeycloakExternalIdentityProfileResolver>>());
        var externalOperatorProvisioner = Substitute.For<IExternalOperatorProvisioner>();
        externalOperatorProvisioner
            .ProvisionAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<ExternalIdentityProfile>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AppUser
            {
                Id = 123,
                AuthProvider = AuthProviders.Keycloak,
                ExternalSubjectId = "123"
            }));

        var logger = Substitute.For<ILogger<KeycloakTokenIntrospectionAuthorization>>();

        var strategy = new KeycloakTokenIntrospectionAuthorization(
            registry,
            options,
            mapper,
            httpContextAccessor,
            tokenIntrospectionService,
            profileResolver,
            externalOperatorProvisioner,
            logger);

        return new TestContext(
            Registry: registry,
            Options: optionsInstance,
            Mapper: mapper,
            HttpContextAccessor: httpContextAccessor,
            HttpClientFactory: httpClientFactory,
            ExternalOperatorProfileReader: externalOperatorProfileReader,
            UserInfoProfileService: userInfoProfileService,
            ExternalOperatorProvisioner: externalOperatorProvisioner,
            Handler: handler,
            Strategy: strategy);
    }

    private sealed record TestContext(
        AuthProviderRegistry Registry,
        KeycloakOptions Options,
        StubExternalAuthorizationMapper Mapper,
        IHttpContextAccessor HttpContextAccessor,
        IHttpClientFactory HttpClientFactory,
        StubExternalOperatorProfileReader ExternalOperatorProfileReader,
        StubKeycloakUserInfoProfileService UserInfoProfileService,
        IExternalOperatorProvisioner ExternalOperatorProvisioner,
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

        public void SetSuccessfulIntrospectionResponse(
            IEnumerable<string> roles,
            string? email = null,
            string? preferredUsername = null,
            string? name = null)
        {
            Dictionary<string, object?> payload = new()
            {
                ["active"] = true,
                ["resource_access"] = new Dictionary<string, object?>
                {
                    [Options.ClientId] = new Dictionary<string, object?>
                    {
                        ["roles"] = roles.ToArray()
                    }
                }
            };

            if (email is not null)
            {
                payload["email"] = email;
            }

            if (preferredUsername is not null)
            {
                payload["preferred_username"] = preferredUsername;
            }

            if (name is not null)
            {
                payload["name"] = name;
            }

            SetHttpResponse(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ConfigurableHttpMessageHandler : HttpMessageHandler
    {
        private HttpResponseMessage _response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"active":true,"resource_access":{}}""", Encoding.UTF8, "application/json")
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

    private sealed class StubExternalAuthorizationMapper : IExternalAuthorizationMapper
    {
        public IExternalAuthorizationMapper.MappingResult Result { get; set; } =
            IExternalAuthorizationMapper.MappingResult.Success(["User"], ["perm.view"]);

        public Task<IExternalAuthorizationMapper.MappingResult> MapToAppRolesAsync(
            string[] externalRoles,
            Dictionary<string, string> roleMappings,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result);
        }
    }

    private sealed class StubExternalOperatorProfileReader : IExternalOperatorProfileReader
    {
        public string? DisplayName { get; set; }

        public Task<string?> GetDisplayNameAsync(
            long tenantId,
            string authProvider,
            string externalSubjectId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(DisplayName);
        }
    }

    private sealed class StubKeycloakUserInfoProfileService : IKeycloakUserInfoProfileService
    {
        public int CallCount { get; private set; }
        public string? AccessToken { get; private set; }
        public Result<ExternalIdentityProfile> ProfileResult { get; set; } =
            Result<ExternalIdentityProfile>.Error("userinfo-not-configured");

        public Task<Result<ExternalIdentityProfile>> GetProfileAsync(
            string accessToken,
            CancellationToken cancellationToken)
        {
            CallCount++;
            AccessToken = accessToken;

            return Task.FromResult(ProfileResult);
        }
    }

    private static void SetProviderIssuer(KeycloakAuthProvider provider, string issuer)
    {
        var field = typeof(KeycloakAuthProvider)
            .GetField("_cachedIssuer", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(provider, issuer);
    }
}


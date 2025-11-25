using System.Reflection;
using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization.Data;
using Endatix.Infrastructure.Identity.Authorization.Strategies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Endatix.Infrastructure.Tests.Identity.Authorization;

public sealed class DefaultAuthorizationTests
{
    private const string TestIssuer = "https://issuer.test";

    [Fact]
    public void CanHandle_ReturnsFalse_WhenIssuerMissing()
    {
        // Arrange
        var context = CreateTestContext();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = context.Strategy.CanHandle(principal);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_ReturnsTrue_WhenIssuerMatchesActiveProvider()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterEndatixProvider(TestIssuer, activate: true);
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
        context.RegisterEndatixProvider(TestIssuer, activate: true);
        var principal = CreatePrincipal("42", "other");

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Provider cannot handle the given issuer");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsError_WhenUserIdMissing()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterEndatixProvider(TestIssuer, activate: true);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Iss, TestIssuer)
        ], "test"));

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("User ID is required");
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_DelegatesToProvider_WhenInputsValid()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterEndatixProvider(TestIssuer, activate: true);
        var principal = CreatePrincipal("123", TestIssuer);
        var expected = AuthorizationData.ForAuthenticatedUser("123", 10, ["Role"], ["perm"]);
        context.AuthorizationDataProvider
            .GetAuthorizationDataAsync(123, Arg.Any<CancellationToken>())
            .Returns(Result.Success(expected));

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        await context.AuthorizationDataProvider.Received(1).GetAuthorizationDataAsync(123, Arg.Any<CancellationToken>());
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public async Task GetAuthorizationDataAsync_ReturnsError_WhenDataProviderFails()
    {
        // Arrange
        var context = CreateTestContext();
        context.RegisterEndatixProvider(TestIssuer, activate: true);
        var principal = CreatePrincipal("123", TestIssuer);
        context.AuthorizationDataProvider
            .GetAuthorizationDataAsync(123, Arg.Any<CancellationToken>())
            .Returns(Result<AuthorizationData>.Error("failed"));

        // Act
        var result = await context.Strategy.GetAuthorizationDataAsync(principal, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("failed");
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
        var authorizationReader = Substitute.For<IAuthorizationDataProvider>();
        var strategy = new DefaultAuthorization(registry, authorizationReader);

        return new TestContext(registry, authorizationReader, strategy);
    }

    private sealed record TestContext(
        AuthProviderRegistry Registry,
        IAuthorizationDataProvider AuthorizationDataProvider,
        DefaultAuthorization Strategy)
    {
        public void RegisterEndatixProvider(string issuer, bool activate)
        {
            var provider = new EndatixJwtAuthProvider();

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"Endatix:Auth:Providers:{provider.SchemeName}:Enabled"] = "true",
                    [$"Endatix:Auth:Providers:{provider.SchemeName}:Issuer"] = issuer,
                    [$"Endatix:Auth:Providers:{provider.SchemeName}:SigningKey"] = "12345678901234567890123456789012",
                    [$"Endatix:Auth:Providers:{provider.SchemeName}:Audiences:0"] = "hub"
                })
                .Build();

            Registry.RegisterProvider<EndatixJwtOptions>(provider, services, configuration);
            SetEndatixIssuer(provider, issuer);

            if (activate)
            {
                Registry.AddActiveProvider(provider);
            }
        }
    }

    private static void SetEndatixIssuer(EndatixJwtAuthProvider provider, string issuer)
    {
        var field = typeof(EndatixJwtAuthProvider)
            .GetField("_cachedIssuer", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(provider, issuer);
    }
}


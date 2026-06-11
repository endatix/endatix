using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.Submitters;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submitters;

public sealed class SubmitterResolverTests
{
    private readonly IRepository<Submitter> _repository = Substitute.For<IRepository<Submitter>>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IUniqueConstraintViolationChecker _uniqueConstraintViolationChecker =
        Substitute.For<IUniqueConstraintViolationChecker>();
    private readonly SubmitterProfileSnapshotBuilder _snapshotBuilder =
        new(Options.Create(new SubmitterOptions()));

    [Fact]
    public async Task ResolveAsync_WithHigherPriorityCustomExtractor_UsesCustomExtractorBeforeBuiltIn()
    {
        SubmitterClaimReader claimReader = new();
        KeycloakSubmitterClaimExtractor keycloakExtractor = new(
            CreateRegistry(AuthProviders.Keycloak, "https://keycloak.test"),
            Options.Create(new SubmitterOptions()),
            claimReader);
        CustomSubmitterClaimExtractor customExtractor = new();
        var resolver = CreateResolver(keycloakExtractor, customExtractor);
        ClaimsPrincipal principal = new(new ClaimsIdentity(
        [
            new Claim(ClaimNames.UserId, "bf89d22f-acbc-4574-bf7d-53dbcf438bb7"),
            new Claim(JwtRegisteredClaimNames.Iss, "https://keycloak.test")
        ], authenticationType: "Keycloak"));

        var resolution = await resolver.ResolveAsync(
            new SubmitterResolveContext(1, principal),
            CancellationToken.None);

        resolution.SubmitterId.Should().BeNull();
        resolution.DisplayId.Should().BeNull();
        resolution.ProfileSnapshot.Should().BeNull();
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task ResolveAsync_WhenInsertRacesWithExistingSubmitter_ReturnsExistingWithoutThrowing()
    {
        const long tenantId = 1;
        const long existingSubmitterId = 42;
        const string externalSubjectId = "subject-123";
        DateTimeOffset now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
        _dateTimeProvider.UtcNow.Returns(now);

        var existingSubmitter = Submitter.Create(
            tenantId,
            AuthProviders.Keycloak,
            externalSubjectId,
            "display-id",
            null,
            null,
            now);
        existingSubmitter.Id = existingSubmitterId;

        _repository
            .SingleOrDefaultAsync(
                Arg.Any<SubmitterSpecifications.ByExternalSubjectSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(null, existingSubmitter);

        _repository
            .AddAsync(Arg.Any<Submitter>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Submitter>(new InvalidOperationException("duplicate key")));

        _uniqueConstraintViolationChecker
            .AnalyzeUniqueConstraint(Arg.Any<Exception>())
            .Returns(new UniqueConstraintViolationResult(
                true,
                Submitter.UniqueConstraints.ExternalSubjectPerTenant,
                null));

        var resolver = CreateResolver();
        var resolution = await resolver.ResolveAsync(
            new SubmitterResolveContext(
                tenantId,
                null,
                new SubmitterInput(
                    externalSubjectId,
                    "display-id",
                    AuthProviders.Keycloak)),
            CancellationToken.None);

        resolution.SubmitterId.Should().Be(existingSubmitterId);
        resolution.DisplayId.Should().Be("display-id");
        await _repository.Received(1).UpdateAsync(existingSubmitter, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenNativeInsertRacesWithExistingSubmitter_ReturnsExistingWithoutThrowing()
    {
        const long tenantId = 1;
        const long existingSubmitterId = 43;
        const long appUserId = 123;
        DateTimeOffset now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
        _dateTimeProvider.UtcNow.Returns(now);

        var existingSubmitter = Submitter.Create(
            tenantId,
            AuthProviders.Endatix,
            null,
            "native-display",
            appUserId,
            null,
            now);
        existingSubmitter.Id = existingSubmitterId;

        _repository
            .SingleOrDefaultAsync(
                Arg.Any<SubmitterSpecifications.ByAppUserSpec>(),
                Arg.Any<CancellationToken>())
            .Returns(null, existingSubmitter);

        _repository
            .AddAsync(Arg.Any<Submitter>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Submitter>(new InvalidOperationException("duplicate key")));

        _uniqueConstraintViolationChecker
            .AnalyzeUniqueConstraint(Arg.Any<Exception>())
            .Returns(new UniqueConstraintViolationResult(
                true,
                Submitter.UniqueConstraints.AppUserPerTenant,
                null));

        var resolver = CreateResolver();
        var resolution = await resolver.ResolveAsync(
            new SubmitterResolveContext(
                tenantId,
                null,
                new SubmitterInput(
                    "native-subject-is-ignored-for-endatix-app-user",
                    "native-display",
                    AuthProviders.Endatix,
                    appUserId)),
            CancellationToken.None);

        resolution.SubmitterId.Should().Be(existingSubmitterId);
        resolution.DisplayId.Should().Be("native-display");
        await _repository.Received(1).UpdateAsync(existingSubmitter, Arg.Any<CancellationToken>());
    }

    private SubmitterResolver CreateResolver(params ISubmitterClaimExtractor[] extractors) =>
        new(
            _repository,
            extractors,
            _snapshotBuilder,
            _dateTimeProvider,
            _uniqueConstraintViolationChecker);

    private sealed class CustomSubmitterClaimExtractor : ISubmitterClaimExtractor
    {
        public int Priority => 0;

        public bool CanExtract(ClaimsPrincipal principal) => principal.Identity?.IsAuthenticated == true;

        public SubmitterExtractionInput Extract(ClaimsPrincipal principal)
        {
            return new SubmitterExtractionInput(
                SubmitterAuthProviders.Anonymous,
                null,
                null,
                null);
        }
    }

    private static AuthProviderRegistry CreateRegistry(string schemeName, string issuer)
    {
        AuthProviderRegistry registry = new();
        TestAuthProvider provider = new(schemeName, issuer);
        registry.RegisterProvider<TestAuthProviderOptions>(
            provider,
            new ServiceCollection(),
            new ConfigurationBuilder().Build());
        registry.AddActiveProvider(provider);

        return registry;
    }

    private sealed class TestAuthProvider(string schemeName, string issuer) : IAuthProvider
    {
        public string SchemeName => schemeName;

        public bool CanHandle(string tokenIssuer, string rawToken) =>
            string.Equals(tokenIssuer, issuer, StringComparison.Ordinal);

        public bool Configure(AuthenticationBuilder builder, IConfigurationSection providerConfig, bool isDevelopment = false) => true;
    }

    private sealed class TestAuthProviderOptions : AuthProviderOptions
    {
    }
}

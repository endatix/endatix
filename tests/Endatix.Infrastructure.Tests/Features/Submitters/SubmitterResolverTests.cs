using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Features.Submitters;
using Endatix.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submitters;

public sealed class SubmitterResolverTests
{
    [Fact]
    public async Task ResolveAsync_WithHigherPriorityCustomExtractor_UsesCustomExtractorBeforeBuiltIn()
    {
        SubmitterClaimReader claimReader = new();
        KeycloakSubmitterClaimExtractor keycloakExtractor = new(Options.Create(new SubmitterOptions()), claimReader);
        CustomSubmitterClaimExtractor customExtractor = new();
        IRepository<Submitter> repository = Substitute.For<IRepository<Submitter>>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        SubmitterProfileSnapshotBuilder snapshotBuilder = new(Options.Create(new SubmitterOptions()));
        SubmitterResolver resolver = new(
            repository,
            [keycloakExtractor, customExtractor],
            snapshotBuilder,
            dateTimeProvider);
        ClaimsPrincipal principal = new(new ClaimsIdentity(
        [
            new Claim(ClaimNames.UserId, "bf89d22f-acbc-4574-bf7d-53dbcf438bb7")
        ], authenticationType: "Keycloak"));

        SubmitterResolution resolution = await resolver.ResolveAsync(
            new SubmitterResolveContext(1, principal),
            CancellationToken.None);

        resolution.SubmitterId.Should().BeNull();
        resolution.DisplayId.Should().BeNull();
        resolution.ProfileSnapshot.Should().BeNull();
        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

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
}

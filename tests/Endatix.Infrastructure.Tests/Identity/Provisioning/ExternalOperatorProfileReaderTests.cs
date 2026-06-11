using Endatix.Infrastructure.Identity.Provisioning;

namespace Endatix.Infrastructure.Tests.Identity.Provisioning;

public sealed class ExternalOperatorProfileReaderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetDisplayNameAsync_WithInvalidAuthProvider_ThrowsArgumentException(string? authProvider)
    {
        ExternalOperatorProfileReader reader = new(null!);

        var action = async () => await reader.GetDisplayNameAsync(
            tenantId: 1,
            authProvider: authProvider!,
            externalSubjectId: "subject-123",
            cancellationToken: CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetDisplayNameAsync_WithInvalidExternalSubjectId_ThrowsArgumentException(string? externalSubjectId)
    {
        ExternalOperatorProfileReader reader = new(null!);

        var action = async () => await reader.GetDisplayNameAsync(
            tenantId: 1,
            authProvider: "Keycloak",
            externalSubjectId: externalSubjectId!,
            cancellationToken: CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
    }
}

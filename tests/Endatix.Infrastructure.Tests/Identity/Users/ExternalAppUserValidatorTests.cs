using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Users;

namespace Endatix.Infrastructure.Tests.Identity.Users;

public sealed class ExternalAppUserValidatorTests
{
    private readonly ExternalAppUserValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WithExternalUserMissingEmail_ReturnsFailedResult()
    {
        AppUser user = new()
        {
            AuthProvider = AuthProviders.Keycloak,
            ExternalSubjectId = "bf89d22f-acbc-4574-bf7d-53dbcf438bb7",
            UserName = "Keycloak:bf89d22f-acbc-4574-bf7d-53dbcf438bb7"
        };

        Microsoft.AspNetCore.Identity.IdentityResult result = await _validator.ValidateAsync(null!, user);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Code == nameof(AppUser.Email));
    }

    [Fact]
    public async Task ValidateAsync_WithExternalUserEmailAndSubject_ReturnsSuccess()
    {
        AppUser user = new()
        {
            AuthProvider = AuthProviders.Keycloak,
            ExternalSubjectId = "bf89d22f-acbc-4574-bf7d-53dbcf438bb7",
            UserName = "Keycloak:bf89d22f-acbc-4574-bf7d-53dbcf438bb7",
            Email = "test@example.com"
        };

        Microsoft.AspNetCore.Identity.IdentityResult result = await _validator.ValidateAsync(null!, user);

        result.Succeeded.Should().BeTrue();
    }
}

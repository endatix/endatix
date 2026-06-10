using Endatix.Infrastructure.Features.Submitters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Features.Submitters;

public sealed class SubmitterOptionsValidatorTests
{
    [Fact]
    public void Validate_WithInvalidClaimType_ReturnsFailure()
    {
        SubmitterOptionsValidator validator = new(CreateConfiguration(keycloakEnabled: true));
        SubmitterOptions options = new()
        {
            DisplayIdClaimTypes = ["preferred username"]
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure => failure.Contains(nameof(SubmitterOptions.DisplayIdClaimTypes)));
    }

    [Fact]
    public void Validate_WithTooManyDisplayClaims_ReturnsFailure()
    {
        SubmitterOptionsValidator validator = new(CreateConfiguration(keycloakEnabled: true));
        SubmitterOptions options = new()
        {
            DisplayIdClaimTypes = Enumerable.Range(1, 21)
                .Select(index => $"claim{index}")
                .ToList()
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure => failure.Contains("cannot contain more than 20 entries"));
    }

    [Fact]
    public void Validate_WithKeycloakEnabledAndEmptyDisplayClaims_ReturnsFailure()
    {
        SubmitterOptionsValidator validator = new(CreateConfiguration(keycloakEnabled: true));
        SubmitterOptions options = new()
        {
            DisplayIdClaimTypes = []
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure => failure.Contains("when Keycloak authentication is enabled"));
    }

    [Fact]
    public void Validate_WithKeycloakDisabledAndEmptyDisplayClaims_ReturnsSuccess()
    {
        SubmitterOptionsValidator validator = new(CreateConfiguration(keycloakEnabled: false));
        SubmitterOptions options = new()
        {
            DisplayIdClaimTypes = []
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidProfileSnapshotField_ReturnsFailure()
    {
        SubmitterOptionsValidator validator = new(CreateConfiguration(keycloakEnabled: false));
        SubmitterOptions options = new()
        {
            DisplayIdClaimTypes = [],
            ProfileSnapshotFields = ["preferred username"]
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure => failure.Contains(nameof(SubmitterOptions.ProfileSnapshotFields)));
    }

    [Fact]
    public void Validate_WithProfileSnapshotFields_ReturnsSuccess()
    {
        SubmitterOptionsValidator validator = new(CreateConfiguration(keycloakEnabled: false));
        SubmitterOptions options = new()
        {
            DisplayIdClaimTypes = [],
            ProfileSnapshotFields = ["email", "given_name"]
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    private static IConfiguration CreateConfiguration(bool keycloakEnabled)
    {
        Dictionary<string, string?> values = new()
        {
            ["Endatix:Auth:Providers:Keycloak:Enabled"] = keycloakEnabled.ToString()
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}

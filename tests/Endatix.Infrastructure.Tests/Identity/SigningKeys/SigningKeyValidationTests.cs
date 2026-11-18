using System.Security.Cryptography;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.SigningKeys;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Identity.SigningKeys;

public class SigningKeyValidationTests
{
    private const string SHIPPED_SUBMISSION_KEY_HASH = "cb5ad31f8f6e274994daca1008c303b68ab654b37e9a9c61e1dc08129a09a2c1";
    private const string DOCS_SIGN_IN_KEY_HASH = "1d58e624acb542bbb785bd62b51ed7eb9dafbbd37fef015e1bd498972b86044c";

    private static string RandomKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private static ValidateOptionsResult ValidateSubmissionKey(string? key, IReadOnlySet<string>? denied = null) =>
        new SigningKeyOptionsValidator<SubmissionAccessTokenOptions>(SigningKeyValidation.SubmissionKeys, denied)
            .Validate(Options.DefaultName, new SubmissionAccessTokenOptions { AccessTokenSigningKey = key! });

    private static ValidateOptionsResult ValidateSignInKey(string? key, IReadOnlySet<string>? denied = null) =>
        new SigningKeyOptionsValidator<EndatixJwtOptions>(SigningKeyValidation.SignInKeys, denied)
            .Validate(Options.DefaultName, new EndatixJwtOptions { SigningKey = key ?? string.Empty });

    [Fact]
    public void Deny_list_holds_the_submission_key_shipped_before_the_fix()
    {
        SigningKeyRule.KnownPublicKeyHashes.Should().Contain(SHIPPED_SUBMISSION_KEY_HASH);
    }

    [Fact]
    public void Deny_list_holds_the_sign_in_key_example_from_the_docs()
    {
        SigningKeyRule.KnownPublicKeyHashes.Should().Contain(DOCS_SIGN_IN_KEY_HASH);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_submission_key_fails_and_names_the_environment_variable(string? key)
    {
        var result = ValidateSubmissionKey(key);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Endatix__Submissions__AccessTokenSigningKey").And.Contain("is not set");
    }

    [Fact]
    public void A_submission_key_under_32_bytes_fails()
    {
        var result = ValidateSubmissionKey(new string('k', 31));

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("shorter than 32 bytes");
    }

    [Fact]
    public void Key_length_is_counted_in_utf8_bytes()
    {
        var sixteenTwoByteChars = new string('é', 16);

        ValidateSubmissionKey(sixteenTwoByteChars).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void A_deny_listed_submission_key_fails_without_echoing_the_value()
    {
        var publicKey = RandomKey();
        var denied = new HashSet<string> { SigningKeyRule.Hash(publicKey) };

        var result = ValidateSubmissionKey(publicKey, denied);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("publicly known").And.NotContain(publicKey);
    }

    [Fact]
    public void A_random_submission_key_passes()
    {
        ValidateSubmissionKey(RandomKey()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void A_missing_sign_in_key_fails_and_names_the_environment_variable()
    {
        var result = ValidateSignInKey(key: null);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Endatix__Auth__Providers__EndatixJwt__SigningKey");
    }

    [Fact]
    public void A_short_sign_in_key_fails()
    {
        ValidateSignInKey(new string('k', 17)).Failed.Should().BeTrue();
    }

    [Fact]
    public void A_random_sign_in_key_passes()
    {
        ValidateSignInKey(RandomKey()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void A_deny_listed_sign_in_key_fails()
    {
        var publicKey = RandomKey();

        var result = ValidateSignInKey(publicKey, new HashSet<string> { SigningKeyRule.Hash(publicKey) });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("publicly known").And.NotContain(publicKey);
    }

    [Fact]
    public void Startup_refuses_a_missing_sign_in_key_when_the_endatix_jwt_provider_is_disabled()
    {
        using var services = SignInKeyStartupServices(signInKey: string.Empty, providerEnabled: false);

        var validate = () => services.GetRequiredService<IStartupValidator>().Validate();

        validate.Should().Throw<OptionsValidationException>()
            .Which.Message.Should().Contain("Endatix__Auth__Providers__EndatixJwt__SigningKey");
    }

    [Fact]
    public void Startup_accepts_a_random_sign_in_key_when_the_endatix_jwt_provider_is_disabled()
    {
        using var services = SignInKeyStartupServices(RandomKey(), providerEnabled: false);

        var validate = () => services.GetRequiredService<IStartupValidator>().Validate();

        validate.Should().NotThrow();
    }

    /// <summary>
    /// The services startup validation runs over: the signing-key options and the EndatixJwt options bound the way the
    /// provider registration binds them, which it does whether or not the provider is enabled.
    /// </summary>
    private static ServiceProvider SignInKeyStartupServices(string signInKey, bool providerEnabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SigningKeyRule.SUBMISSION_KEY_PATH] = RandomKey(),
                [SigningKeyRule.JWT_KEY_PATH] = signInKey,
                ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = providerEnabled.ToString(),
            })
            .Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration);
        services.AddSigningKeyOptions(configuration);
        new AuthProviderRegistry().RegisterProvider<EndatixJwtOptions>(new EndatixUserJwtAuthProvider(), services, configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void A_legacy_sign_in_key_fills_an_empty_sign_in_key_in_every_environment()
    {
        var legacyKey = RandomKey();
        var configuration = new ConfigurationManager();
        configuration[SigningKeyRule.LEGACY_JWT_KEY_PATH] = legacyKey;

        SigningKeyValidation.ResolveSigningKeys(configuration, isDevelopment: false, NullLogger.Instance);

        configuration[SigningKeyRule.JWT_KEY_PATH].Should().Be(legacyKey);
    }

    [Fact]
    public void A_sign_in_key_that_is_set_wins_over_the_legacy_key()
    {
        var signInKey = RandomKey();
        var configuration = new ConfigurationManager();
        configuration[SigningKeyRule.JWT_KEY_PATH] = signInKey;
        configuration[SigningKeyRule.LEGACY_JWT_KEY_PATH] = RandomKey();

        SigningKeyValidation.ResolveSigningKeys(configuration, isDevelopment: false, NullLogger.Instance);

        configuration[SigningKeyRule.JWT_KEY_PATH].Should().Be(signInKey);
    }

    [Fact]
    public void Development_without_keys_gets_one_random_key_per_signing_key()
    {
        var configuration = new ConfigurationManager();

        SigningKeyValidation.ResolveSigningKeys(configuration, isDevelopment: true, NullLogger.Instance);

        var submissionKey = configuration[SigningKeyRule.SUBMISSION_KEY_PATH];
        var signInKey = configuration[SigningKeyRule.JWT_KEY_PATH];
        ValidateSubmissionKey(submissionKey).Succeeded.Should().BeTrue();
        ValidateSignInKey(signInKey).Succeeded.Should().BeTrue();
        submissionKey.Should().NotBe(signInKey);
    }

    [Fact]
    public void Development_treats_whitespace_keys_as_missing_and_gets_random_keys()
    {
        var configuration = new ConfigurationManager();
        configuration[SigningKeyRule.SUBMISSION_KEY_PATH] = "   ";
        configuration[SigningKeyRule.JWT_KEY_PATH] = "   ";

        SigningKeyValidation.ResolveSigningKeys(configuration, isDevelopment: true, NullLogger.Instance);

        ValidateSubmissionKey(configuration[SigningKeyRule.SUBMISSION_KEY_PATH]).Succeeded.Should().BeTrue();
        ValidateSignInKey(configuration[SigningKeyRule.JWT_KEY_PATH]).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void A_whitespace_legacy_sign_in_key_does_not_fill_the_sign_in_key()
    {
        var configuration = new ConfigurationManager();
        configuration[SigningKeyRule.LEGACY_JWT_KEY_PATH] = "   ";

        SigningKeyValidation.ResolveSigningKeys(configuration, isDevelopment: true, NullLogger.Instance);

        ValidateSignInKey(configuration[SigningKeyRule.JWT_KEY_PATH]).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void A_legacy_sign_in_key_fills_a_whitespace_sign_in_key()
    {
        var legacyKey = RandomKey();
        var configuration = new ConfigurationManager();
        configuration[SigningKeyRule.JWT_KEY_PATH] = "   ";
        configuration[SigningKeyRule.LEGACY_JWT_KEY_PATH] = legacyKey;

        SigningKeyValidation.ResolveSigningKeys(configuration, isDevelopment: false, NullLogger.Instance);

        configuration[SigningKeyRule.JWT_KEY_PATH].Should().Be(legacyKey);
    }

    [Fact]
    public void Development_keeps_keys_that_are_set_even_when_they_are_bad()
    {
        var shortKey = new string('k', 10);
        var configuration = new ConfigurationManager();
        configuration[SigningKeyRule.SUBMISSION_KEY_PATH] = shortKey;
        configuration[SigningKeyRule.LEGACY_JWT_KEY_PATH] = shortKey;

        SigningKeyValidation.ResolveSigningKeys(configuration, isDevelopment: true, NullLogger.Instance);

        configuration[SigningKeyRule.SUBMISSION_KEY_PATH].Should().Be(shortKey);
        configuration[SigningKeyRule.JWT_KEY_PATH].Should().Be(shortKey);
    }

    [Fact]
    public void Outside_development_missing_keys_stay_missing()
    {
        var configuration = new ConfigurationManager();

        SigningKeyValidation.ResolveSigningKeys(configuration, isDevelopment: false, NullLogger.Instance);

        configuration[SigningKeyRule.SUBMISSION_KEY_PATH].Should().BeNull();
        configuration[SigningKeyRule.JWT_KEY_PATH].Should().BeNull();
    }
}

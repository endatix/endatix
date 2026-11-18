using System.Security.Cryptography;
using Endatix.Framework.Configuration;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Identity.SigningKeys;

/// <summary>
/// Wires the signing-key rule into startup: the API refuses to start on a missing, short or publicly known key.
/// In Development only, a key that is not set at all is replaced by a random key for the life of the process.
/// </summary>
internal static class SigningKeyValidation
{
    /// <summary>Binds the submission token options and validates the submission and sign-in signing keys when the host starts.</summary>
    public static IServiceCollection AddSigningKeyOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndatixOptions<SubmissionAccessTokenOptions>(configuration);
        services.AddSingleton<IValidateOptions<SubmissionAccessTokenOptions>>(
            new SigningKeyOptionsValidator<SubmissionAccessTokenOptions>(SubmissionKeys));
        services.AddOptions<SubmissionAccessTokenOptions>().ValidateOnStart();

        // Bound by the EndatixJwt provider's registration. Token issuing and public form links sign with this key, so it
        // gets the same startup rule as the submission key.
        services.AddSingleton<IValidateOptions<EndatixJwtOptions>>(new SigningKeyOptionsValidator<EndatixJwtOptions>(SignInKeys));
        services.AddOptions<EndatixJwtOptions>().ValidateOnStart();

        return services;
    }

    internal static IEnumerable<SigningKeySetting> SubmissionKeys(SubmissionAccessTokenOptions options) =>
        [new SigningKeySetting(SigningKeyRule.SUBMISSION_KEY_PATH, options.AccessTokenSigningKey)];

    internal static IEnumerable<SigningKeySetting> SignInKeys(EndatixJwtOptions options) =>
        [new SigningKeySetting(SigningKeyRule.JWT_KEY_PATH, options.SigningKey)];

    /// <summary>
    /// Settles the signing keys in configuration before anything reads them, so the token issuers and the bearer
    /// validators all see the same key:
    /// <list type="bullet">
    /// <item>a legacy <c>Endatix:Jwt:SigningKey</c> fills an empty sign-in key;</item>
    /// <item>in Development only, a key that is still not set gets a random value for this process, with a warning.
    /// Tokens it signs stop working on restart, and each replica gets its own key.</item>
    /// </list>
    /// </summary>
    public static void ResolveSigningKeys(IConfiguration configuration, bool isDevelopment, ILogger logger)
    {
        var legacyKey = configuration[SigningKeyRule.LEGACY_JWT_KEY_PATH];
        if (string.IsNullOrWhiteSpace(configuration[SigningKeyRule.JWT_KEY_PATH]) && !string.IsNullOrWhiteSpace(legacyKey))
        {
            AddToConfiguration(configuration, new Dictionary<string, string?> { [SigningKeyRule.JWT_KEY_PATH] = legacyKey });
        }

        if (isDevelopment)
        {
            UseRandomKeysWhenMissing(configuration, logger);
        }
    }

    private static void UseRandomKeysWhenMissing(IConfiguration configuration, ILogger logger)
    {
        var generated = MissingKeyPaths(configuration).ToDictionary(path => path, _ => (string?)RandomKey());
        if (generated.Count == 0)
        {
            return;
        }

        AddToConfiguration(configuration, generated);
        foreach (var path in generated.Keys)
        {
            LogRandomKey(logger, path);
        }
    }

    private static void LogRandomKey(ILogger logger, string path) =>
        logger.LogWarning(
            "{ConfigPath} is not set. Using a random key for this process only (Development); tokens it signs stop working on restart, and each replica gets its own key. {HowToFix}",
            path,
            SigningKeyRule.HowToFix(path));

    private static IEnumerable<string> MissingKeyPaths(IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration[SigningKeyRule.SUBMISSION_KEY_PATH]))
        {
            yield return SigningKeyRule.SUBMISSION_KEY_PATH;
        }

        if (string.IsNullOrWhiteSpace(configuration[SigningKeyRule.JWT_KEY_PATH]))
        {
            yield return SigningKeyRule.JWT_KEY_PATH;
        }
    }

    /// <summary>A source added last wins over every earlier one and survives a reload of the JSON files.</summary>
    private static void AddToConfiguration(IConfiguration configuration, Dictionary<string, string?> values)
    {
        if (configuration is IConfigurationBuilder builder)
        {
            builder.AddInMemoryCollection(values);
            return;
        }

        foreach (var (path, value) in values)
        {
            configuration[path] = value;
        }
    }

    private static string RandomKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}

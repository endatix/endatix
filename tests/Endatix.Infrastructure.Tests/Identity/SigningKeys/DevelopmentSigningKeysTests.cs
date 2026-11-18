using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.SigningKeys;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Identity.SigningKeys;

/// <summary>
/// The OSS host and a fresh <c>dotnet new endatix-api</c> project start in Development: their committed Development
/// settings set no signing key, so the Development fallback gives them random keys. Values are read at run time and
/// never printed.
/// </summary>
public class DevelopmentSigningKeysTests
{
    private static readonly string _templateDirectory = Path.Combine(RepoRoot(), "templates", "endatix-api");

    public static TheoryData<string> SettingsDirectories => new()
    {
        Path.Combine("templates", "endatix-api"),
        Path.Combine("src", "Endatix.WebHost"),
    };

    [Theory]
    [MemberData(nameof(SettingsDirectories))]
    public void The_development_settings_set_no_signing_key(string settingsDirectory)
    {
        // Arrange
        var development = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(RepoRoot(), settingsDirectory))
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        // Act
        var setKeyPaths = new[] { SigningKeyRule.SUBMISSION_KEY_PATH, SigningKeyRule.JWT_KEY_PATH, SigningKeyRule.LEGACY_JWT_KEY_PATH }
            .Where(path => !string.IsNullOrEmpty(development[path]))
            .ToList();

        // Assert
        setKeyPaths.Should().BeEquivalentTo(Array.Empty<string>());
    }

    [Theory]
    [MemberData(nameof(SettingsDirectories))]
    public void The_development_settings_resolve_to_valid_signing_keys(string settingsDirectory)
    {
        // Arrange
        var configuration = new ConfigurationManager();
        configuration.SetBasePath(Path.Combine(RepoRoot(), settingsDirectory))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: false);

        // Act
        SigningKeyValidation.ResolveSigningKeys(configuration, isDevelopment: true, NullLogger.Instance);

        // Assert
        var submission = new SigningKeyOptionsValidator<SubmissionAccessTokenOptions>(SigningKeyValidation.SubmissionKeys)
            .Validate(Options.DefaultName, new SubmissionAccessTokenOptions { AccessTokenSigningKey = configuration[SigningKeyRule.SUBMISSION_KEY_PATH]! });
        var signIn = new SigningKeyOptionsValidator<EndatixJwtOptions>(SigningKeyValidation.SignInKeys)
            .Validate(Options.DefaultName, new EndatixJwtOptions { SigningKey = configuration[SigningKeyRule.JWT_KEY_PATH] ?? string.Empty });
        submission.Succeeded.Should().BeTrue(submission.FailureMessage);
        signIn.Succeeded.Should().BeTrue(signIn.FailureMessage);
    }

    [Fact]
    public void The_template_settings_ship_no_published_signing_key()
    {
        // Arrange
        var shipped = new ConfigurationBuilder()
            .SetBasePath(_templateDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        // Act
        var publishedKeyPaths = new[] { SigningKeyRule.SUBMISSION_KEY_PATH, SigningKeyRule.JWT_KEY_PATH, SigningKeyRule.LEGACY_JWT_KEY_PATH }
            .Where(path => !string.IsNullOrEmpty(shipped[path]) && SigningKeyRule.KnownPublicKeyHashes.Contains(SigningKeyRule.Hash(shipped[path]!)))
            .ToList();

        // Assert
        publishedKeyPaths.Should().BeEmpty();
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Endatix.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Endatix.slnx not found above the test output.");
    }
}

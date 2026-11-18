using System.Text;
using Endatix.Core;
using Endatix.Core.Branding;
using Endatix.Core.Configuration;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Email;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Endatix.IntegrationTests;

/// <summary>
/// Renders every seeded email from a freshly migrated database. Without a brand pack the result is pinned to a
/// snapshot recorded before brand variables existed, so a change to the seed rows, the renderer or the composed
/// metadata that alters a byte for the built-in brand fails here. Set <c>ENDATIX_UPDATE_EMAIL_SNAPSHOTS=1</c> to
/// re-record it after an intended change.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class SeededEmailSnapshotTests(DbIntegrationFixture fixture)
{
    private const string Recipient = "user@example.com";
    private const string HubBaseUrl = "https://hub.example.com";

    // Both providers seed the same text, so they share one snapshot. The project copies it to the test output.
    private const string SnapshotFile = "Snapshots/seeded-emails.txt";

    private static readonly DeploymentBrand ExampleBrand = new(
        ProductName: "Example Forms",
        AppName: "Example Forms",
        AssistantName: "Example Assistant",
        WebsiteUrl: "https://example.com",
        Email: new BrandEmail("Example Forms Team", "noreply@example.com", "help@example.com"));

    [Fact]
    public async Task Seeded_emails_without_a_brand_pack_match_the_recorded_snapshot()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var emails = await RenderEveryEmailAsync(DeploymentBrand.BuiltIn, cancellationToken);

        // Assert
        var actual = Snapshot(emails);
        if (Environment.GetEnvironmentVariable("ENDATIX_UPDATE_EMAIL_SNAPSHOTS") == "1")
        {
            await File.WriteAllTextAsync(SourceSnapshotPath(), actual, cancellationToken);
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, SnapshotFile);
        File.Exists(path).Should().BeTrue($"the snapshot {SnapshotFile} is committed with this test and copied to the output");
        var expected = await File.ReadAllTextAsync(path, cancellationToken);
        actual.Should().Be(expected.ReplaceLineEndings("\n"));
        emails.Should().OnlyContain(rendered => rendered.Email.FromName == null, "providers keep the sender name they sent before");
    }

    [Fact]
    public async Task Seeded_emails_with_a_brand_pack_show_its_product_sender_and_support_address()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var emails = await RenderEveryEmailAsync(ExampleBrand, cancellationToken);

        // Assert
        emails.Should().HaveCount(6);
        foreach (var (templateId, email) in emails)
        {
            var text = $"{email.Subject}\n{email.HtmlBody}\n{email.PlainTextBody}";
            text.Should().NotContain("Endatix", $"{templateId} names the pack's product only");
            text.Should().Contain("Example Forms", $"{templateId} names the pack's product");
            email.From.Should().Be("noreply@example.com", $"{templateId} sends from the pack's address");
            email.FromName.Should().Be("Example Forms Team", $"{templateId} carries the pack's sender name");
        }

        emails.Single(rendered => rendered.TemplateId == "password-changed").Email.PlainTextBody
            .Should().Contain("help@example.com", "the password-changed email points people to the pack's support address");
    }

    private async Task<List<(string TemplateId, EmailWithBody Email)>> RenderEveryEmailAsync(
        DeploymentBrand brand,
        CancellationToken cancellationToken)
    {
        // A database of its own: the shared fixture's reset empties the seeded EmailTemplates rows.
        await using var database = await JobsTestDatabase.CreateAsync(fixture, cancellationToken);
        await using var provider = (ServiceProvider)IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            database.ConnectionString,
            database.Provider);
        await provider.ApplyDbMigrationsAsync(NullLogger.Instance, cancellationToken);

        using var scope = provider.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        EfRepository<EmailTemplate> repository = new(appDb, new EndatixSpecificationEvaluator([]));
        var settings = Options.Create(ShippedEmailTemplateSettings());
        EmailTemplateService composer = new(settings, Options.Create(new HubSettings { HubBaseUrl = HubBaseUrl }), brand);
        EmailTemplateRenderer renderer = new(repository, settings, brand);

        List<(string TemplateId, EmailWithBody Email)> emails = [];
        foreach (var email in ComposeEveryEmail(composer))
        {
            emails.Add((email.TemplateId, await renderer.RenderAsync(email, cancellationToken)));
        }

        return emails;
    }

    private static IEnumerable<EmailWithTemplate> ComposeEveryEmail(EmailTemplateService composer)
    {
        yield return composer.CreateVerificationEmail(Recipient, "verification-token");
        yield return composer.CreateInvitationEmail(Recipient, "invitation-token");
        yield return composer.CreateForgotPasswordEmail(Recipient, "reset-token");
        yield return composer.CreatePasswordChangedEmail(Recipient);
        yield return SignupEmail(EmailTemplateSettings.TenantSignupRequestTemplateId);
        yield return SignupEmail(EmailTemplateSettings.TenantSignupApprovedTemplateId);
    }

    // The saas waitlist module composes these two; every placeholder the seed rows use gets a marker value.
    private static EmailWithTemplate SignupEmail(string templateId) => new()
    {
        To = Recipient,
        TemplateId = templateId,
        Metadata = new Dictionary<string, object>
        {
            ["actionText"] = "<actionText>",
            ["bodyText"] = "<bodyText>",
            ["companyName"] = "<companyName>",
            ["headline"] = "<headline>",
            ["inboxUrl"] = "https://hub.example.com/inbox",
            ["requestEmail"] = "request@example.com",
            ["signInUrl"] = "https://hub.example.com/signin",
        },
    };

    private static EmailTemplateSettings ShippedEmailTemplateSettings()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();
        EmailTemplateSettings settings = new();
        configuration.GetSection("Endatix:EmailTemplates").Bind(settings);
        return settings;
    }

    private static string Snapshot(IEnumerable<(string TemplateId, EmailWithBody Email)> emails)
    {
        StringBuilder snapshot = new();
        foreach (var (templateId, email) in emails)
        {
            snapshot.Append("=== ").Append(templateId).Append('\n');
            snapshot.Append("From: ").Append(email.From).Append('\n');
            snapshot.Append("To: ").Append(email.To).Append('\n');
            snapshot.Append("Subject: ").Append(email.Subject).Append('\n');
            snapshot.Append("--- html\n").Append(ShowCarriageReturns(email.HtmlBody)).Append('\n');
            snapshot.Append("--- text\n").Append(ShowCarriageReturns(email.PlainTextBody)).Append('\n');
        }

        return snapshot.ToString();
    }

    // A carriage return in a row is part of the bytes sent, so it is kept visible instead of normalized away.
    private static string ShowCarriageReturns(string body) => body.Replace("\r", "\\r", StringComparison.Ordinal);

    // Re-recording writes to the source tree, which exists only in a local build: CI maps source paths to "/_/".
    private static string SourceSnapshotPath([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "") =>
        Path.Combine(Path.GetDirectoryName(sourceFile)!, SnapshotFile);
}

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
/// metadata that alters a byte for the built-in brand fails here. The seed rows still carry the literal product
/// name; a later release moves them to <c>{{productName}}</c>, once no running release lacks the variable. Set <c>ENDATIX_UPDATE_EMAIL_SNAPSHOTS=1</c> to
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

    private static readonly DeploymentBrand _exampleBrand = new(
        ProductName: "Example Forms",
        AppName: "Example Forms",
        AssistantName: "Example Assistant",
        WebsiteUrl: "https://example.com",
        SupportAddress: "help@example.com");

    // The shipped default: no sender configured.
    private static readonly EmailSenderSettings _noSenderConfigured = new();

    [Fact]
    public async Task Seeded_emails_without_a_brand_pack_match_the_recorded_snapshot()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var emails = await RenderEveryEmailAsync(DeploymentBrand.BuiltIn, _noSenderConfigured, cancellationToken);

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
        emails.Should().OnlyContain(
            rendered => rendered.Email.FromName == "Endatix",
            "the sender name defaults to the brand name; SMTP sent it before, and Mailgun and SendGrid now send it too");
    }

    [Fact]
    public async Task Seeded_emails_with_a_brand_pack_and_a_sender_send_from_it_named_after_the_pack()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var sender = new EmailSenderSettings { Address = "noreply@example.com" };

        // Act
        var emails = await RenderEveryEmailAsync(_exampleBrand, sender, cancellationToken);

        // Assert
        emails.Should().HaveCount(6);
        emails.Should().OnlyContain(
            rendered => rendered.Email.FromName == "Example Forms",
            "the sender name defaults to the brand name");
        emails.Should().OnlyContain(
            rendered => rendered.Email.From == "noreply@example.com",
            "a configured sender address wins over every seeded row's address");
        Email(emails, "user-invitation").HtmlBody
            .Should().Contain("Accept your Example Forms invitation").And.Contain("sign in to Example Forms.");
    }

    private static EmailWithBody Email(List<(string TemplateId, EmailWithBody Email)> emails, string templateId) =>
        emails.Single(rendered => rendered.TemplateId == templateId).Email;

    private async Task<List<(string TemplateId, EmailWithBody Email)>> RenderEveryEmailAsync(
        DeploymentBrand brand,
        EmailSenderSettings senderSettings,
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
        var variables = EmailVariables.From(brand);
        EmailTemplateService composer = new(settings, Options.Create(new HubSettings { HubBaseUrl = HubBaseUrl }), variables);
        EmailTemplateRenderer renderer = new(repository, settings, variables, DeploymentSender.From(senderSettings, brand));

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

    // A host module composes these two; every placeholder the seed rows use gets a marker value.
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

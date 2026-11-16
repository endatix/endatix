using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The <c>BrandSeededEmailTemplates</c> migration on both providers: it brands only rows still equal to their seed,
/// and its Down restores them.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class BrandSeededEmailTemplatesMigrationTests(DbIntegrationFixture fixture)
{
    private const string MigrationName = "BrandSeededEmailTemplates";
    private const string EditedSubject = "Reset your password for our forms";

    [Fact]
    public async Task Up_brands_unedited_rows_and_keeps_an_edited_row()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using JobsTestDatabase database = await JobsTestDatabase.CreateAsync(fixture, cancellationToken);
        await using ServiceProvider provider = (ServiceProvider)IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            database.ConnectionString,
            database.Provider);
        using IServiceScope scope = provider.CreateScope();
        AppDbContext appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IMigrator migrator = appDb.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore(appDb), cancellationToken: cancellationToken);
        await EditForgotPasswordSubjectAsync(appDb, cancellationToken);
        string editedHtml = (await TemplateAsync(appDb, "forgot-password", cancellationToken)).HtmlContent;

        // Act
        await migrator.MigrateAsync(cancellationToken: cancellationToken);

        // Assert
        EmailTemplate edited = await TemplateAsync(appDb, "forgot-password", cancellationToken);
        edited.Subject.Should().Be(EditedSubject);
        edited.HtmlContent.Should().Be(editedHtml, "an edited row keeps all of its text");

        EmailTemplate verification = await TemplateAsync(appDb, "email-verification", cancellationToken);
        verification.Subject.Should().Be("Verify your email to activate your {{productName}} account");
        verification.HtmlContent.Should().NotContain("Endatix");

        EmailTemplate passwordChanged = await TemplateAsync(appDb, "password-changed", cancellationToken);
        passwordChanged.PlainTextContent.Should().Contain("{{supportAddress}}").And.NotContain("support@endatix.com");
        passwordChanged.FromAddress.Should().Be("noreply@endatix.com", "the migration does not touch the sender");
    }

    [Fact]
    public async Task Down_restores_the_seeded_text()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using JobsTestDatabase database = await JobsTestDatabase.CreateAsync(fixture, cancellationToken);
        await using ServiceProvider provider = (ServiceProvider)IntegrationCoreMigrationTestHelper.BuildServiceProvider(
            database.ConnectionString,
            database.Provider);
        using IServiceScope scope = provider.CreateScope();
        AppDbContext appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IMigrator migrator = appDb.GetService<IMigrator>();
        string previous = MigrationBefore(appDb);
        await migrator.MigrateAsync(previous, cancellationToken: cancellationToken);
        List<EmailTemplate> seeded = await TemplatesAsync(appDb, cancellationToken);
        await migrator.MigrateAsync(cancellationToken: cancellationToken);

        // Act
        await migrator.MigrateAsync(previous, cancellationToken: cancellationToken);

        // Assert
        List<EmailTemplate> restored = await TemplatesAsync(appDb, cancellationToken);
        restored.Select(Text).Should().Equal(seeded.Select(Text));
    }

    private static string MigrationBefore(AppDbContext appDb)
    {
        List<string> migrations = appDb.Database.GetMigrations().ToList();
        int index = migrations.FindIndex(id => id.EndsWith($"_{MigrationName}", StringComparison.Ordinal));
        index.Should().BePositive($"the {MigrationName} migration exists");
        return migrations[index - 1];
    }

    private static Task EditForgotPasswordSubjectAsync(AppDbContext appDb, CancellationToken cancellationToken) =>
        appDb.Set<EmailTemplate>()
            .Where(template => template.Name == "forgot-password")
            .ExecuteUpdateAsync(update => update.SetProperty(template => template.Subject, EditedSubject), cancellationToken);

    private static Task<EmailTemplate> TemplateAsync(AppDbContext appDb, string name, CancellationToken cancellationToken) =>
        appDb.Set<EmailTemplate>().AsNoTracking().SingleAsync(template => template.Name == name, cancellationToken);

    private static Task<List<EmailTemplate>> TemplatesAsync(AppDbContext appDb, CancellationToken cancellationToken) =>
        appDb.Set<EmailTemplate>().AsNoTracking().OrderBy(template => template.Name).ToListAsync(cancellationToken);

    private static string Text(EmailTemplate template) =>
        $"{template.Name}\n{template.Subject}\n{template.HtmlContent}\n{template.PlainTextContent}";
}

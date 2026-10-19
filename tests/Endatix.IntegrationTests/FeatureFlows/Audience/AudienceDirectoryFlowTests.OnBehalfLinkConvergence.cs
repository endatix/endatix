using System.Net;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// An integration sends only the subject, the person's imported identifier. The on-behalf create and the person's
/// link resolve to one Submitter, labelled with the subject, whichever path comes first.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Creating_on_behalf_with_only_a_subject_personalises_and_binds_the_imported_person()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var (formId, identifier) = await FormWithImportedPersonAsync(admin, "Columbus");

        // Act
        using var created = await SubjectOnlyOnBehalfAsync(admin, formId, identifier);

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await admin.ReadAsync(created);
        Assert.Equal(identifier, body.GetProperty("submitterDisplayId").GetString());
        Assert.Equal("on_behalf", body.GetProperty("personalization").GetProperty("source").GetString());
        Assert.Equal("Columbus", body.GetProperty("personalization").GetProperty("variables").GetProperty("city").GetString());
        Assert.Equal(IdOf(body, "submitterId"), await BoundSubmitterIdAsync(world.Services, identifier));
    }

    [Fact]
    public async Task A_subject_only_on_behalf_after_the_link_opened_shares_its_submitter_and_conflicts()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var (formId, identifier) = await FormWithImportedPersonAsync(admin, "Dayton", limitOnePerUser: true);
        using var anonymous = world.AnonymousClient();
        var opened = await RedeemAsync(anonymous, formId, await IssueLinkAsync(admin, formId));
        var linkSubmissionId = IdOf(opened, "submissionId");

        // Act
        using var onBehalf = await SubjectOnlyOnBehalfAsync(admin, formId, identifier);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, onBehalf.StatusCode);
        var problem = await admin.ReadAsync(onBehalf);
        Assert.Equal("submission_exists", problem.GetProperty("errorCode").GetString());
        Assert.Equal(linkSubmissionId, IdOf(problem, "submissionId"));
        var read = await ReadSubmissionAsync(admin, formId, linkSubmissionId);
        Assert.Equal(await BoundSubmitterIdAsync(world.Services, identifier), IdOf(read, "submitterId"));
    }

    [Fact]
    public async Task A_link_opened_after_a_subject_only_on_behalf_resumes_it_and_a_second_create_conflicts()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var (formId, identifier) = await FormWithImportedPersonAsync(admin, "Toledo", limitOnePerUser: true);
        using var created = await SubjectOnlyOnBehalfAsync(admin, formId, identifier);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await admin.ReadAsync(created);
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, formId, await IssueLinkAsync(admin, formId));
        using var again = await SubjectOnlyOnBehalfAsync(admin, formId, identifier);

        // Assert
        Assert.False(opened.GetProperty("created").GetBoolean());
        Assert.Equal(IdOf(body, "id"), IdOf(opened, "submissionId"));
        var read = await ReadSubmissionAsync(admin, formId, IdOf(body, "id"));
        Assert.Equal(IdOf(body, "submitterId"), IdOf(read, "submitterId"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("submission_exists", (await admin.ReadAsync(again)).GetProperty("errorCode").GetString());
    }

    /// <summary>A form with a City property and one person imported from a CSV with their city.</summary>
    private static async Task<(long FormId, string Identifier)> FormWithImportedPersonAsync(
        AudienceApi admin,
        string city,
        bool limitOnePerUser = false)
    {
        var formId = await admin.CreateFormAsync(limitOnePerUser);
        await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var identifier = $"{Guid.NewGuid():N}@example.com";
        using var imported = await admin.ImportAsync(
            formId, $"email,City\n{identifier},{city}\n", new Dictionary<string, string> { ["city"] = "City" });
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Equal(1, (await admin.ReadAsync(imported)).GetProperty("createdCount").GetInt32());
        return (formId, identifier);
    }

    private static Task<HttpResponseMessage> SubjectOnlyOnBehalfAsync(AudienceApi admin, long formId, string subject) =>
        admin.PostAsync(
            $"/api/forms/{formId}/submissions/onbehalf",
            new { submitter = new { externalSubjectId = subject } });

    private static async Task<long?> BoundSubmitterIdAsync(IServiceProvider services, string identifier)
    {
        await using var scope = services.CreateAsyncScope();
        var audience = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        return await audience.Members.AsNoTracking()
            .Where(member => member.Identifier == identifier)
            .Select(member => member.SubmitterId)
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}

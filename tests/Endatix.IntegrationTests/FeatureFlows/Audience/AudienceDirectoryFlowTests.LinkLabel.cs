using System.Net;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// The response a link open starts carries the person's label, as an on-behalf one does: the label of the
/// Submitter bound to the person, else their identifier.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Opening_a_link_labels_the_new_submission_with_the_persons_identifier()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Canton");
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, link.FormId, link.Token);
        var read = await ReadSubmissionAsync(admin, link.FormId, IdOf(opened, "submissionId"));

        // Assert
        Assert.True(opened.GetProperty("created").GetBoolean());
        Assert.Equal(link.Identifier, read.GetProperty("submitterDisplayId").GetString());
    }

    /// <summary>An on-behalf create bound the person to a Submitter with its own label; the link's response uses it.</summary>
    [Fact]
    public async Task Opening_a_link_labels_the_new_submission_with_the_bound_submitters_label()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var link = await LinkForPersonWithCityAsync(admin, "Lorain");
        using var created = await admin.PostAsync(
            $"/api/forms/{link.FormId}/submissions/onbehalf",
            new { submitter = new { externalSubjectId = link.Identifier, displayId = "Ada Lovelace", authProvider = "Integration" } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var onBehalfId = IdOf(await admin.ReadAsync(created), "id");
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, link.FormId, link.Token);
        var read = await ReadSubmissionAsync(admin, link.FormId, IdOf(opened, "submissionId"));

        // Assert
        Assert.NotEqual(onBehalfId, IdOf(opened, "submissionId"));
        Assert.Equal("Ada Lovelace", read.GetProperty("submitterDisplayId").GetString());
    }
}

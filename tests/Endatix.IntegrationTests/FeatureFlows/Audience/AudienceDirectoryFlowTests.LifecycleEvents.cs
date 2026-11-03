using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.IntegrationTests.Infrastructure;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// The audience's create entry points, a personalised link's first open and a test submission, each capture one
/// <c>submission.created</c> outbox row.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Redeeming_a_link_captures_one_created_row_and_a_reopen_none()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var (formId, token) = await LinkForPersonWithCityAsync(admin, "Columbus");
        using var anonymous = world.AnonymousClient();

        // Act
        var opened = await RedeemAsync(anonymous, formId, token);
        await RedeemAsync(anonymous, formId, token);

        // Assert
        var rows = await SubmissionOutboxRows.ReadAsync(
            world.Services, IdOf(opened, "submissionId"), TestContext.Current.CancellationToken);
        var row = Assert.Single(rows);
        Assert.Equal(SubmissionCreatedEvent.EventTypeName, row.EventType);
        Assert.Equal(CollectionStatusCodes.InProgress, row.CollectionStatus);
        Assert.False(row.IsTestSubmission);
    }

    [Fact]
    public async Task Opening_a_test_submission_captures_one_created_row_flagged_as_test()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var person = await LinkForPersonWithCityAsync(admin, "Columbus");

        // Act
        var opened = await OpenTestSubmissionAsync(admin, person.FormId, MembershipIdOf(person));

        // Assert
        var rows = await SubmissionOutboxRows.ReadAsync(
            world.Services, IdOf(opened, "submissionId"), TestContext.Current.CancellationToken);
        var row = Assert.Single(rows);
        Assert.Equal(SubmissionCreatedEvent.EventTypeName, row.EventType);
        Assert.True(row.IsTestSubmission);
    }
}

using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Issuing_a_link_checks_account_standing_but_redeeming_it_while_signed_in_does_not()
    {
        // Arrange
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(AuthorizationData.ForAuthenticatedUser("owner", 1, [], [])));
        await using var host = new ReportingWebHost(fixture, services =>
            services.AddTransient(_ => new AccountStanding(snapshots)));
        var world = await WorldAsync(host);
        var owner = await AsAsync(world, TestPersona.TenantAdmin);
        var (formId, _) = await owner.AddPersonToNewFormAsync("participant@example.com");
        snapshots.ClearReceivedCalls();

        // Act
        var token = await IssueLinkAsync(owner, formId);
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        snapshots.ClearReceivedCalls();
        var opened = await RedeemAsync(owner.Client, formId, token);

        // Assert
        Assert.True(opened.GetProperty("created").GetBoolean());
        await AssertFillsInAsync(owner.Client, (formId, IdOf(opened, "submissionId")), SubmissionAccessTokenOf(opened));
        await snapshots.DidNotReceive().GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
    }
}

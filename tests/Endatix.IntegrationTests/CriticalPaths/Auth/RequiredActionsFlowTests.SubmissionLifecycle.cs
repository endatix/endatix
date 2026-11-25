using System.Net;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deleting_a_form_retires_warm_and_cold_managed_submission_access(bool enforce)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            if (enforce)
            {
                EnableStanding(services);
            }
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var warm = await SubmissionTarget.CreateAsync(owner, ct);
        var cold = await warm.SiblingAsync(owner, ct);
        await AssertContentStatusAsync(owner, warm.LifecycleRequests(), HttpStatusCode.OK);

        await using var writerHost = new ReportingWebHost(fixture);
        using var writer = writerHost.CreateClient();
        writer.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        using var deleted = await writer.DeleteAsync(warm.FormRoute, ct);

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        await AssertContentStatusAsync(owner, warm.LifecycleRequests(), HttpStatusCode.NotFound);
        await AssertContentStatusAsync(owner, cold.LifecycleRequests(), HttpStatusCode.NotFound);
    }
}

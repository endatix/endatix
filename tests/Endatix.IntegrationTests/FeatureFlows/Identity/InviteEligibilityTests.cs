using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.UseCases.Identity.InviteUser;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests.FeatureFlows.Identity;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class InviteEligibilityTests(EndatixIntegrationWebHostFixture fixture)
{
    [Theory]
    [InlineData(InviteEligibility.Declined, HttpStatusCode.Conflict, "This workspace is not eligible to invite teammates yet.")]
    [InlineData(InviteEligibility.Unavailable, HttpStatusCode.ServiceUnavailable, "Invite eligibility is unavailable. Try again.")]
    public async Task Module_refusals_return_an_invite_problem_before_registration(
        InviteEligibility decision, HttpStatusCode status, string detail)
    {
        var ct = TestContext.Current.CancellationToken;
        var registration = Substitute.For<IUserRegistrationService>();
        var eligibility = Substitute.For<IInviteEligibilityProvider>();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton(registration);
            services.AddSingleton(eligibility);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        eligibility.DecideAsync(world.Tenants[0].Id, Arg.Any<CancellationToken>()).Returns(decision);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);

        using var response = await admin.PostAsJsonAsync("/api/users", new { email = "teammate@example.test" }, ct);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(detail, problem.GetProperty("detail").GetString());
        if (decision == InviteEligibility.Declined)
        {
            Assert.Equal("invite_declined", problem.GetProperty("errorCode").GetString());
        }
        else
        {
            Assert.False(problem.TryGetProperty("errorCode", out _));
        }
        Assert.False(problem.TryGetProperty("requiredActions", out _));
        await eligibility.Received(1).DecideAsync(world.Tenants[0].Id, Arg.Any<CancellationToken>());
        Assert.Empty(registration.ReceivedCalls());
    }
}

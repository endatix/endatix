using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class RequiredActionsFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    [Fact]
    public async Task Me_lists_the_pending_action_for_the_signed_in_subject()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        var tenant = world.Tenants[0];
        var provider = new ProfileProvider(tenant.Admin.Id.ToString(), tenant.Id);
        await using var root = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        await using var host = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IRequiredActionProvider>(provider)));
        using var signedIn = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;

        // Act
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);

        // Assert
        var action = Assert.Single(me.GetProperty("requiredActions").EnumerateArray());
        Assert.Equal("profile.complete", action.GetProperty("key").GetString());
        Assert.Equal("/profile", action.GetProperty("resolvePath").GetString());
        Assert.Equal("user", action.GetProperty("scope").GetString());
        Assert.Equal("blocking", action.GetProperty("severity").GetString());
        Assert.True(action.GetProperty("canResolve").GetBoolean());
    }

    private sealed class ProfileProvider(string userId, long tenantId) : IRequiredActionProvider
    {
        public Task<IReadOnlyList<RequiredAction>> GetPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken)
        {
            Assert.Equal(userId, subject.UserId);
            Assert.Equal(tenantId, subject.TenantId);
            Assert.Contains(SystemRole.Admin.Name, subject.Roles);
            return Task.FromResult<IReadOnlyList<RequiredAction>>([new()
            {
                Key = "profile.complete", ResolvePath = "/profile",
                Scope = RequiredActionScope.User, Severity = RequiredActionSeverity.Blocking
            }]);
        }
    }
}

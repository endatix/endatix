using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Features.Email;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Provisioning_and_teammate_invites_use_distinct_registration_paths()
    {
        const string email = "pending@example.test";
        var ct = TestContext.Current.CancellationToken;
        var provider = new MutableProvider("legal", "/legal");
        var reads = new TargetLookups(email);
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppIdentityDbContext>(options => options.AddInterceptors(reads));
            services.AddSingleton(Substitute.For<IEmailSender>());
            services.AddSingleton<IRequiredActionProvider>(provider);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var scope = world.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<IUserRegistrationService>();
        var pending = await provisioning.RegisterInvitedUserAsync(email, world.Tenants[0].Id, ct);
        Assert.True(pending.IsSuccess);

        reads.Reset();
        using var refused = await admin.PostAsJsonAsync("/api/users", new { email }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(0, reads.Count);
        provider.Pending = false;
        await InvalidateAsync(world, ct);

        reads.Reset();
        using var allowed = await admin.PostAsJsonAsync("/api/users", new { email }, ct);
        allowed.EnsureSuccessStatusCode();
        Assert.Equal(1, reads.Count);
        var reinvited = await allowed.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(pending.Value.Id.ToString(), reinvited.GetProperty("id").ToString());
    }

    private sealed class TargetLookups(string email) : DbCommandInterceptor
    {
        public int Count { get; private set; }
        public void Reset() => Count = 0;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("NormalizedEmail", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Any(p => string.Equals(p.Value as string, email, StringComparison.OrdinalIgnoreCase)))
            {
                Count++;
            }
            return ValueTask.FromResult(result);
        }
    }
}

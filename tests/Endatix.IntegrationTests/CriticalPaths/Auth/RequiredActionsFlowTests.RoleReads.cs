using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Refused_role_changes_skip_the_create_name_lookup_and_product_entities()
    {
        var ct = TestContext.Current.CancellationToken;
        var reads = new RoleProductReads();
        var provider = new MutableProvider("legal", "/legal");
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppIdentityDbContext>(options => options.AddInterceptors(reads));
            services.AddSingleton<IRequiredActionProvider>(provider);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        var tenant = world.Tenants[0].Id;
        await world.Seed.SeedRoleAsync(tenant, "Editable", ct);
        await world.Seed.SeedRolePermissionsAsync(tenant, "Editable", [Actions.Access.Hub], ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        var role = new { name = "ProposedRole", permissions = new[] { Actions.Access.Hub } };
        reads.Reset();
        using var blockedCreate = await admin.PostAsJsonAsync("/api/roles", role, ct);
        await AssertStandingRefusalAsync(blockedCreate, me, ct);
        Assert.Equal((0, 0, 0), reads.Counts);
        using var blockedUpdate = await admin.PutAsJsonAsync("/api/roles/Editable", role, ct);
        await AssertStandingRefusalAsync(blockedUpdate, me, ct);
        Assert.Equal((0, 0, 0), reads.Counts);
        Assert.Equal(0, reads.EditableRoleLookups);

        provider.Pending = false;
        await InvalidateAsync(world, ct);
        using var created = await admin.PostAsJsonAsync("/api/roles", role, ct);
        created.EnsureSuccessStatusCode();
        Assert.True(reads.Counts.Names > 0 && reads.Counts.Permissions > 0);
        reads.Reset();
        using var updated = await admin.PutAsJsonAsync("/api/roles/Editable", role, ct);
        updated.EnsureSuccessStatusCode();
        Assert.True(reads.Counts.Roles > 0 && reads.Counts.Permissions > 0);
        Assert.Equal(1, reads.EditableRoleLookups);
    }

    [Theory]
    [InlineData(0, HttpStatusCode.BadRequest, "Cannot update system-defined role 'Editorial'.")]
    [InlineData(1, HttpStatusCode.NotFound, "Role 'Editorial' not found for this tenant.")]
    public async Task Admitted_role_updates_preserve_system_and_tenant_refusals(
        int targetTenant, HttpStatusCode status, string detail)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, EnableStanding);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        var targetId = world.Tenants[targetTenant].Id;
        await world.Seed.SeedRoleAsync(targetId, "Editorial", ct);
        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var role = await db.Roles.SingleAsync(r => r.TenantId == targetId && r.Name == "Editorial", ct);
        role.IsSystemDefined = true;
        role.Description = "Original";
        await db.SaveChangesAsync(ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);

        using var response = await admin.PutAsJsonAsync("/api/roles/Editorial", new { description = "Changed" }, ct);

        Assert.Equal(status, response.StatusCode);
        Assert.Contains(detail, await response.Content.ReadAsStringAsync(ct));
        await db.Entry(role).ReloadAsync(ct);
        Assert.Equal("Original", role.Description);
    }

    private sealed class RoleProductReads : DbCommandInterceptor, IMaterializationInterceptor
    {
        public (int Names, int Roles, int Permissions) Counts { get; private set; }

        public int EditableRoleLookups { get; private set; }

        public void Reset()
        {
            Counts = default;
            EditableRoleLookups = 0;
        }

        public object InitializedInstance(MaterializationInterceptionData data, object entity)
        {
            Counts = (Counts.Names, Counts.Roles + (entity is AppRole ? 1 : 0), Counts.Permissions + (entity is Permission ? 1 : 0));
            return entity;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.Parameters.Cast<DbParameter>().Any(p => p.Value is "EDITABLE"))
            {
                EditableRoleLookups++;
            }
            if (command.CommandText.Contains("EXISTS", StringComparison.OrdinalIgnoreCase)
                && command.Parameters.Cast<DbParameter>().Any(p => p.Value is "PROPOSEDROLE"))
            {
                Counts = (Counts.Names + 1, Counts.Roles, Counts.Permissions);
            }
            return ValueTask.FromResult(result);
        }
    }
}

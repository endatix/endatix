using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Theory]
    [InlineData("list")]
    [InlineData("detail")]
    [InlineData("replace")]
    [InlineData("patch")]
    [InlineData("delete")]
    public async Task Managed_forms_refuse_the_pending_owner_and_resume_after_clearance(string operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var action = new MutableProvider("terms", "/terms") { Pending = false };
        var product = new FormProductReads();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(product));
            services.AddSingleton<IRequiredActionProvider>(action);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var id = await CreateFormAsync(owner, ct);
        var before = await owner.GetFromJsonAsync<JsonElement>($"/api/forms/{id}", ct);
        Assert.True(product.Reads > 0);
        action.Pending = true;
        await InvalidateAsync(world, ct);
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);

        product.Reset();
        using var blocked = await ManagedFormRequestAsync(owner, operation, id);
        await AssertStandingRefusalAsync(blocked, me, ct);
        Assert.Equal(0, product.Reads);

        action.Pending = false;
        await InvalidateAsync(world, ct);
        var after = await owner.GetFromJsonAsync<JsonElement>($"/api/forms/{id}", ct);
        Assert.True(JsonElement.DeepEquals(before, after));
        using var resumed = await ManagedFormRequestAsync(owner, operation, id);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
    }

    [Fact]
    public async Task Unavailable_form_standing_follows_ordinary_visibility_and_permission_checks()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var second = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var visible = await CreateFormAsync(owner, ct);
        var hidden = await CreateFormAsync(second, ct);
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        using var client = host.CreateClient();
        using var restricted = await RestrictedUserAsync(world, ct);
        client.DefaultRequestHeaders.Authorization = restricted.DefaultRequestHeaders.Authorization;
        using var denied = await client.GetAsync("/api/forms", ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        foreach (var operation in new[] { "detail", "replace", "patch", "delete" })
        {
            using var missing = await ManagedFormRequestAsync(client, operation, long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var invisible = await ManagedFormRequestAsync(client, operation, hidden);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, invisible.StatusCode);
        }
        Assert.Empty(snapshots.ReceivedCalls());
        await AssertUnavailableOperationsAsync(client, visible, snapshots);
    }

    private static async Task AssertUnavailableOperationsAsync(HttpClient client, string visible, ICurrentUserAuthorizationService snapshots)
    {
        var ct = TestContext.Current.CancellationToken;
        foreach (var operation in new[] { "list", "detail", "replace", "patch", "delete" })
        {
            snapshots.ClearReceivedCalls();
            using var unavailable = await ManagedFormRequestAsync(client, operation, visible);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
            var problem = await unavailable.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Contains("Try again", problem.GetProperty("detail").GetString());
            await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(true, RequiredActionSeverity.Notice)]
    [InlineData(true, null)]
    [InlineData(false, RequiredActionSeverity.Blocking)]
    public async Task Managed_forms_allow_notices_no_provider_and_dormant_hosts(bool enforce, RequiredActionSeverity? severity)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            if (severity.HasValue)
            {
                services.AddSingleton<IRequiredActionProvider>(new MutableProvider("setup", "/setup") { Severity = severity.Value });
            }
            if (enforce)
            {
                EnableStanding(services);
            }
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var form = await CreateFormAsync(owner, ct);
        foreach (var operation in new[] { "list", "detail", "replace", "patch", "delete" })
        {
            using var response = await ManagedFormRequestAsync(owner, operation, form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Managed_form_writes_preserve_folder_and_theme_availability_before_standing()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var form = await CreateFormAsync(owner, ct);
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        var route = $"/api/forms/{form}";
        var absent = long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var folder = new { name = "Moved form", folderId = absent, isEnabled = true };

        using var replace = await client.PutAsJsonAsync(route, folder, ct);
        using var patch = await client.PatchAsJsonAsync(route, folder, ct);
        using var theme = await client.PatchAsJsonAsync(route, new { themeId = long.MaxValue }, ct);

        // The folder policy's existing error maps to 500; missing themes map to 404.
        Assert.Equal(HttpStatusCode.InternalServerError, replace.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, patch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, theme.StatusCode);
        Assert.Empty(snapshots.ReceivedCalls());
    }

    private sealed class FormProductReads : DbCommandInterceptor
    {
        public int Reads { get; private set; }
        public void Reset() => Reads = 0;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            var sql = command.CommandText;
            if (sql.Contains("Submissions", StringComparison.OrdinalIgnoreCase) || ReadsFormData(sql))
            {
                Reads++;
            }
            return ValueTask.FromResult(result);
        }

        private static bool ReadsFormData(string sql) =>
            sql.Contains("Forms", StringComparison.OrdinalIgnoreCase)
            && (sql.Contains("WebHookSettingsJson", StringComparison.OrdinalIgnoreCase) || sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase));
    }

    private static Task<HttpResponseMessage> ManagedFormRequestAsync(HttpClient owner, string operation, string id)
    {
        var ct = TestContext.Current.CancellationToken;
        const string forms = "/api/forms";
        var detail = $"{forms}/{id}";
        var change = new { name = "New form name", isEnabled = true };
        return operation switch
        {
            "list" => owner.GetAsync(forms, ct),
            "detail" => owner.GetAsync(detail, ct),
            "replace" => owner.PutAsJsonAsync(detail, change, ct),
            "patch" => owner.PatchAsJsonAsync(detail, change, ct),
            "delete" => owner.DeleteAsync(detail, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }
}

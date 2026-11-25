using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.FeatureFlows.DataLists;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Managed_data_lists_refuse_product_work_and_resume_export_after_clearance()
    {
        var ct = TestContext.Current.CancellationToken;
        var action = new MutableProvider("setup", "/setup") { Pending = false };
        var commands = new DataListCommands();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(commands));
            services.AddSingleton<IRequiredActionProvider>(action);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var item = await CreateDataListAsync(owner, ct);
        using var imported = await owner.PutAsJsonAsync($"{item}/import", new { format = "csv", csv = "value,default\r\na,Apple\r\n" }, ct);
        imported.EnsureSuccessStatusCode();
        action.Pending = true;
        await InvalidateAsync(world, ct);
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.True(commands.ProductWork > 0);
        commands.ProductWork = 0;
        foreach (var request in DataListRequests(item))
        {
            using var sent = request;
            using var response = await owner.SendAsync(sent, ct);
            await AssertStandingRefusalAsync(response, me, ct);
            Assert.Null(response.Content.Headers.ContentDisposition);
        }
        Assert.Equal(0, commands.ProductWork);
        action.Pending = false;
        await InvalidateAsync(world, ct);
        using var exported = await owner.PostAsJsonAsync($"{item}/export", new { format = "csv" }, ct);
        exported.EnsureSuccessStatusCode();
        Assert.Contains("a,Apple", await exported.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Managed_data_list_operations_check_standing_once_and_form_token_lookups_skip_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(ct).ReturnsForAnyArgs(Result.Success(new AuthorizationData()));
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var (form, list) = await FormAccessTokenUseFlowTests.SeedRuntimeFormAsync(world, true, ct);
        snapshots.GetAuthorizationDataAsync(ct).ReturnsForAnyArgs(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        foreach (var request in DataListRequests($"/api/data-lists/{list}"))
        {
            using var sent = request;
            snapshots.ClearReceivedCalls();
            using var refused = await owner.SendAsync(sent, ct);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
            await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        }
        snapshots.ClearReceivedCalls();
        using var respondent = await PublicDataListScopeFlowTests.AnonymousWithFormTokenAsync(world, form, ct);
        var search = await respondent.GetStringAsync(PublicDataListScopeFlowTests.SearchUrl(form, list), ct);
        var display = await respondent.GetStringAsync(PublicDataListScopeFlowTests.DisplayValuesUrl(form, list, "bound-choice"), ct);
        Assert.Contains("bound-choice", search);
        Assert.Contains("Bound label", display);
        Assert.Empty(snapshots.ReceivedCalls());
    }

    private static async Task<string> CreateDataListAsync(HttpClient owner, CancellationToken ct)
    {
        using var created = await owner.PostAsJsonAsync("/api/data-lists", new { name = "Fruit" }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var list = await created.Content.ReadFromJsonAsync<JsonElement>(ct);
        return $"/api/data-lists/{list.GetProperty("id")}";
    }

    private static IEnumerable<HttpRequestMessage> DataListRequests(string item)
    {
        yield return new(HttpMethod.Get, "/api/data-lists");
        yield return new(HttpMethod.Get, "/api/data-lists/locales");
        yield return new(HttpMethod.Post, "/api/data-lists") { Content = JsonContent.Create(new { name = "Cities" }) };
        yield return new(HttpMethod.Get, item);
        yield return new(HttpMethod.Get, $"{item}/items");
        yield return new(HttpMethod.Get, $"{item}/forms");
        yield return new(HttpMethod.Post, $"{item}/export") { Content = JsonContent.Create(new { format = "json" }) };
        yield return new(HttpMethod.Put, $"{item}/import") { Content = JsonContent.Create(new { items = Array.Empty<object>() }) };
        yield return new(HttpMethod.Post, $"{item}/locales") { Content = JsonContent.Create(new { locale = "es" }) };
        yield return new(HttpMethod.Delete, $"{item}/locales/es");
        yield return new(HttpMethod.Put, $"{item}/default-locale") { Content = JsonContent.Create(new { defaultLocale = "es" }) };
        yield return new(HttpMethod.Patch, item) { Content = JsonContent.Create(new { name = "Changed" }) };
        yield return new(HttpMethod.Delete, item);
    }
}

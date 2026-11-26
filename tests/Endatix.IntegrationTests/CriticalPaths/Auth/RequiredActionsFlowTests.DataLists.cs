using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
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
    public async Task Admitted_and_dormant_data_list_reads_do_the_same_product_work()
    {
        var dormant = await ReadDataListAsync(false);
        var admitted = await ReadDataListAsync(true);
        Assert.True(dormant > 0);
        Assert.Equal(dormant, admitted);
    }

    private async Task<int> ReadDataListAsync(bool enforce)
    {
        var ct = TestContext.Current.CancellationToken;
        var commands = new DataListCommands();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppDbContext>(commands.Observe);
            if (enforce)
            {
                EnableStanding(services);
            }
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var item = await CreateDataListAsync(owner, ct);
        commands.ProductWork = 0;
        var list = await owner.GetFromJsonAsync<JsonElement>(item, ct);
        Assert.Equal("Fruit", list.GetProperty("name").GetString());
        return commands.ProductWork;
    }

    [Fact]
    public async Task Managed_data_lists_refuse_product_work_and_resume_export_after_clearance()
    {
        var ct = TestContext.Current.CancellationToken;
        var action = new MutableProvider("setup", "/setup") { Pending = false };
        var commands = new DataListCommands();
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.ConfigureDbContext<AppDbContext>(commands.Observe);
            services.AddSingleton<IRequiredActionProvider>(action);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var item = await CreateDataListAsync(owner, ct);
        using var imported = await owner.PutAsJsonAsync($"{item}/import", new { format = "csv", csv = "value,default\r\na,Apple\r\n" }, ct);
        imported.EnsureSuccessStatusCode();
        var before = await owner.GetFromJsonAsync<JsonElement>(item, ct);
        var formWork = commands.FormWork;
        using var dependencies = await owner.GetAsync($"{item}/forms", ct);
        dependencies.EnsureSuccessStatusCode();
        Assert.True(commands.FormWork > formWork);
        action.Pending = true;
        await InvalidateAsync(world, ct);
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.True(commands.ProductWork > 0);
        commands.ProductWork = 0;
        formWork = commands.FormWork;
        foreach (var request in DataListRequests(item))
        {
            using var sent = request;
            using var response = await owner.SendAsync(sent, ct);
            await AssertStandingRefusalAsync(response, me, ct);
            Assert.Null(response.Content.Headers.ContentDisposition);
        }
        Assert.Equal(0, commands.ProductWork);
        Assert.Equal(formWork, commands.FormWork);
        action.Pending = false;
        await InvalidateAsync(world, ct);
        var after = await owner.GetFromJsonAsync<JsonElement>(item, ct);
        Assert.True(JsonElement.DeepEquals(before, after));
        using var exported = await owner.PostAsJsonAsync($"{item}/export", new { format = "csv" }, ct);
        exported.EnsureSuccessStatusCode();
        Assert.Contains("a,Apple", await exported.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Managed_data_list_operations_check_standing_once_and_form_token_lookups_skip_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(ct).ReturnsForAnyArgs(Result.Success(AdmittedSnapshot()));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Managed_search_checks_visibility_before_calling_the_search_repository(bool enforced)
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = Substitute.For<IDataListRepository>();
        repository.SearchItemsAsync(Arg.Any<DataListSearchCriteria>(), Arg.Any<CancellationToken>())
            .Returns(call => new DataListSearchPageResult(call.Arg<DataListSearchCriteria>().DataListId, 0, []));
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddScoped(_ => repository);
            if (enforced)
            {
                EnableStanding(services);
            }
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var first = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var second = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var local = await CreateDataListAsync(first, ct);
        var foreign = await CreateDataListAsync(second, ct, "Vegetables");
        using var ownSearch = await first.GetAsync($"{local}/items", ct);
        using var foreignSearch = await first.GetAsync($"{foreign}/items", ct);
        Assert.Equal(HttpStatusCode.OK, ownSearch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignSearch.StatusCode);
        await repository.Received(1).SearchItemsAsync(Arg.Any<DataListSearchCriteria>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Managed_data_list_catalogs_return_only_the_current_tenants_lists_and_locales()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, EnableStanding);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var first = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var second = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        await CreateDataListAsync(first, ct);
        var foreign = await CreateDataListAsync(second, ct, "Vegetables");
        using var added = await second.PostAsJsonAsync($"{foreign}/locales", new { locale = "fr" }, ct);
        added.EnsureSuccessStatusCode();
        var catalog = await first.GetFromJsonAsync<JsonElement>("/api/data-lists", ct);
        var locales = await first.GetFromJsonAsync<string[]>("/api/data-lists/locales", ct);
        Assert.Equal("Fruit", Assert.Single(catalog.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
        Assert.NotNull(locales);
        Assert.Equal(["en"], locales);
        using var unbound = world.Services.CreateScope();
        var ambient = unbound.ServiceProvider.GetRequiredService<AppDbContext>().GetTenantId();
        Assert.Equal(0, ambient);
        var repository = unbound.ServiceProvider.GetRequiredService<IDataListRepository>();
        Assert.Empty(await repository.ListDistinctLocalesAsync(ct));
    }

    [Fact]
    public async Task Admitted_managed_list_operations_require_a_list_in_the_current_tenant()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, EnableStanding);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var first = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var second = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var foreign = await CreateDataListAsync(second, ct);

        // Act / Assert
        foreach (var operation in DataListRequests(foreign))
        {
            using var request = operation;
            if (request.RequestUri!.OriginalString.StartsWith(foreign, StringComparison.Ordinal))
            {
                using var response = await first.SendAsync(request, ct);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }
        using var stillVisible = await second.GetAsync(foreign, ct);
        stillVisible.EnsureSuccessStatusCode();
    }

    private static async Task<string> CreateDataListAsync(HttpClient owner, CancellationToken ct, string name = "Fruit")
    {
        using var created = await owner.PostAsJsonAsync("/api/data-lists", new { name }, ct);
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
